// Organigrama page — loads the org chart via Google Charts
// Issue #286: agrega switches de filtro (mostrar código, mostrar unidades
// vigentes) y exportaciones PNG/PDF. El render se redespliega cada vez que
// el usuario cambia un switch; los exports capturan el estado visual actual
// del chart, incluyendo los nodos que quedaron colapsados manualmente.
(function () {
    'use strict';

    var chartDiv = document.getElementById('orgchart');
    if (!chartDiv) return;

    var showCodeInput = document.getElementById('toggle-show-code');
    var showExpiradasInput = document.getElementById('toggle-show-expiradas');
    var exportPngBtn = document.getElementById('btn-export-png');
    var exportPdfBtn = document.getElementById('btn-export-pdf');

    // Estado vivo de los switches. Se inicializa desde los checkboxes
    // (que arrancan `checked` en el HTML) y se mantiene sincronizado
    // con el `change` event. Cualquier acción (export, redraw) lee
    // desde acá para evitar inconsistencias con el DOM.
    //
    // `showExpiradas` controla las unidades cuya ventana de vigencia
    // ya cerró (issue #286 — feedback del usuario): las vigentes se
    // muestran SIEMPRE; cuando el switch está OFF se ocultan las
    // expiradas y cuando está ON se muestran todas.
    var options = {
        showCode: !showCodeInput || showCodeInput.checked === true,
        showExpiradas: !showExpiradasInput || showExpiradasInput.checked === true
    };

    // Referencia al chart activo. La exportacion PNG/PDF lo consume
    // directamente; se asigna en drawOrgChart y queda null cuando no
    // hay árbol renderizado (estado vacío, error, o filtro que oculta
    // todos los nodos vigentes).
    var currentChart = null;

    // Timeout: si Google Charts no carga en 10 segundos, mostramos
    // error. El timeout se cancela apenas el chart se renderiza OK
    // o cuando la carga falla con errorCallback.
    var timeoutId = setTimeout(function () {
        console.error('[OrgChart] Timeout: Google Charts no cargó en 10 segundos');
        if (chartDiv) {
            chartDiv.innerHTML = '<div class="text-center text-muted py-5"><p>No se pudo cargar el organigrama (timeout de Google Charts).</p></div>';
        }
    }, 10000);

    if (typeof google === 'undefined') {
        console.error('[OrgChart] ERROR: google is undefined. El CDN de Google Charts no cargó.');
        chartDiv.innerHTML = '<div class="text-center text-muted py-5"><p>No se pudo cargar el organigrama (CDN de Google Charts no disponible).</p></div>';
        bindEvents();
        return;
    }

    /**
     * Aplica los switches de filtro al árbol pre-cargado. Devuelve un
     * árbol NUEVO (no muta `nodes`) para que los toggles del usuario
     * puedan dispararse varias veces seguidas sin arrastrar estado
     * entre renders.
     *
     * Reglas (issue #286):
     *  - `showVigentes === false` → descartar toda la sub-jerarquía
     *    de un nodo no vigente (hijos también se ocultan) para evitar
     *    nodos huérfanos sin padre visible.
     *  - `showVigentes === true` (default) → conservar todo.
     */
    function applyFilters(nodes) {
        if (!nodes) return [];
        var result = [];
        for (var i = 0; i < nodes.length; i++) {
            var node = nodes[i];
            // Issue #286 (revisión): el switch "Mostrar unidades expiradas"
            // controla exclusivamente la visibilidad de las unidades cuya
            // ventana de vigencia ya cerró. Las vigentes (esVigente === true)
            // se muestran SIEMPRE. Cuando `showExpiradas === false`
            // (switch en OFF) descartamos toda la sub-jerarquía del nodo
            // expirado para evitar huérfanos sin padre visible.
            var keepNode = options.showExpiradas || node.esVigente === true;
            if (!keepNode) {
                continue;
            }

            var filteredChildren = applyFilters(node.children || []);
            // Copia superficial para no mutar la entrada; preserva los
            // campos que el JS necesita (id, codigo, nombre, tipo,
            // children, esVigente) y descarta el resto del viewmodel
            // (`vigencia` no se renderiza en el chart).
            result.push({
                id: node.id,
                codigo: node.codigo,
                nombre: node.nombre,
                tipo: node.tipo,
                esVigente: node.esVigente,
                children: filteredChildren
            });
        }
        return result;
    }

    function drawOrgChart() {
        clearTimeout(timeoutId);

        try {
            // El organigrama se hidrata desde datos pre-cargados server-side
            // (ver Organigrama.cshtml: window.__sgvTreeData). Pegar a la API
            // desde el browser daría 401 porque el JWT vive en la cookie
            // httpOnly y ApiBearerTokenHandler solo aplica del lado servidor.
            var treeData = window.__sgvTreeData || [];

            if (!treeData || treeData.length === 0) {
                chartDiv.innerHTML = '<div class="text-center text-muted py-5"><p>No hay unidades organizativas para mostrar en el organigrama.</p></div>';
                currentChart = null;
                return;
            }

            var filtered = applyFilters(treeData);
            if (filtered.length === 0) {
                // El árbol pre-cargado tiene nodos pero todos quedaron fuera
                // del filtro actual (típicamente: switch `showVigentes` en OFF
                // y todas las unidades vencidas). No tiene sentido exportar
                // un chart vacío; currentChart queda null y los botones
                // detectan ese caso vía getImageURI devolviendo string vacío.
                chartDiv.innerHTML = '<div class="text-center text-muted py-5"><p>No hay unidades organizativas vigentes para mostrar con el filtro actual.</p></div>';
                currentChart = null;
                return;
            }

            var data = new google.visualization.DataTable();
            data.addColumn('string', 'Name');
            data.addColumn('string', 'Manager');
            data.addColumn('string', 'ToolTip');

            function flattenTree(nodes, parentId) {
                for (var i = 0; i < nodes.length; i++) {
                    var node = nodes[i];
                    var nodeId = String(node.id);
                    var displayName = options.showCode
                        ? node.codigo + ' \u2014 ' + node.nombre
                        : node.nombre;
                    var tooltip = node.codigo + ' \u00B7 ' + node.tipo;
                    data.addRow([{ v: nodeId, f: displayName }, parentId ? String(parentId) : '', tooltip]);
                    if (node.children && node.children.length > 0) {
                        flattenTree(node.children, nodeId);
                    }
                }
            }

            flattenTree(filtered, null);

            currentChart = new google.visualization.OrgChart(chartDiv);
            currentChart.draw(data, {
                allowHtml: true,
                allowCollapse: true,
                size: 'medium'
            });
        } catch (err) {
            console.error('[OrgChart] ERROR:', err);
            chartDiv.innerHTML = '<div class="text-center text-muted py-5"><p>No se pudo cargar el organigrama. Revisa la consola para más detalles.</p></div>';
            currentChart = null;
        }
    }

    /**
     * Descarga el chart actual como PNG. Usa `chart.getImageURI()` de
     * Google Charts que devuelve un data URI `image/png;base64,...`.
     *
     * Importante: NO anclo el data URI directamente en `<a href>`.
     * Los navegadores modernos (Chrome ≥65, Firefox ≥67, Safari) bloquean
     * la descarga de `data:` URIs disparada por un anchor inyectado
     * dinámicamente (la navegación queda como "no permite navegar a esa
     * URL" y el botón no produce ningún efecto visible). La fix estándar
     * es decodificar el base64, envolverlo en un Blob con el MIME type
     * detectado, y obtener una URL blob: que sí se puede descargar.
     *
     * El nombre de archivo lleva la fecha en formato `YYYYMMDD` (zona
     * horaria del cliente) para que varias exportaciones del mismo día
     * convivan sin pisarse cuando el navegador resuelve colisiones.
     */
    function exportPng() {
        if (!currentChart) {
            console.warn('[OrgChart] exportPng: chart no disponible.');
            return;
        }
        var imgUri = currentChart.getImageURI();
        if (!imgUri) {
            console.warn('[OrgChart] exportPng: getImageURI devolvió vacío.');
            return;
        }

        var commaIdx = imgUri.indexOf(',');
        if (commaIdx < 0 || imgUri.indexOf('data:') !== 0) {
            console.warn('[OrgChart] exportPng: data URI con formato inesperado.');
            return;
        }
        var header = imgUri.substring(5, commaIdx); // after "data:"
        var payload = imgUri.substring(commaIdx + 1);
        var mimeType = header.split(';')[0] || 'image/png';
        var isBase64 = header.indexOf('base64') >= 0;

        var bytes;
        if (isBase64) {
            var binary = atob(payload);
            bytes = new Uint8Array(binary.length);
            for (var i = 0; i < binary.length; i++) {
                bytes[i] = binary.charCodeAt(i);
            }
        } else {
            // data:image/png;charset=... sin base64 → URL-encoded; raro pero
            // la API de Google Charts usa base64, así que este branch es
            // defensa por si cambia el formato.
            bytes = new TextEncoder().encode(decodeURIComponent(payload));
        }

        var blob = new Blob([bytes], { type: mimeType });
        var blobUrl = URL.createObjectURL(blob);

        var now = new Date();
        var yyyymmdd = now.getFullYear().toString()
            + String(now.getMonth() + 1).padStart(2, '0')
            + String(now.getDate()).padStart(2, '0');
        var a = document.createElement('a');
        a.href = blobUrl;
        a.download = 'organigrama-' + yyyymmdd + '.png';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        // Liberamos la URL en el siguiente tick para que el click ya haya
        // consumido el blob antes de invalidarlo.
        setTimeout(function () { URL.revokeObjectURL(blobUrl); }, 0);
    }

    /**
     * Dispara el diálogo nativo de impresión del navegador. El usuario
     * elige "Guardar como PDF" en el diálogo. La regla `@media print`
     * embebida en `Organigrama.cshtml` ya oculta la toolbar, los
     * switches y el shell visual (sidenav, topbar, footer) gracias a
     * `.d-print-none` de Bootstrap + la regla específica del container.
     */
    function exportPdf() {
        window.print();
    }

    /**
     * Vincula los handlers de switches y botones. Se llama apenas el
     * DOM está listo (sin esperar a Google Charts), así los botones
     * están activos desde el primer paint y solo no-op cuando el chart
     * todavía no terminó de cargar.
     */
    function bindEvents() {
        if (showCodeInput) {
            showCodeInput.addEventListener('change', function () {
                options.showCode = showCodeInput.checked;
                drawOrgChart();
            });
        }
        if (showExpiradasInput) {
            showExpiradasInput.addEventListener('change', function () {
                options.showExpiradas = showExpiradasInput.checked;
                drawOrgChart();
            });
        }
        if (exportPngBtn) {
            exportPngBtn.addEventListener('click', exportPng);
        }
        if (exportPdfBtn) {
            exportPdfBtn.addEventListener('click', exportPdf);
        }
    }

    bindEvents();

    google.charts.load('current', {
        packages: ['orgchart'],
        callback: drawOrgChart,
        errorCallback: function (err) {
            clearTimeout(timeoutId);
            console.error('[OrgChart] ERROR: google.charts.load failed:', err);
            chartDiv.innerHTML = '<div class="text-center text-muted py-5"><p>No se pudo cargar el organigrama (error al cargar Google Charts).</p></div>';
        }
    });
})();