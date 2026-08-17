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
    /**
     * Cuenta total / vigentes / expiradas del árbol. Solo para
     * diagnóstico en consola cuando el filtro deja el árbol vacío
     * (issue #286). Si ves `vigentes === 0` en consola pero esperás
     * vigentes, hay un bug server-side: el cálculo de `EsVigente`
     * está retornando false para todas las unidades.
     */
    function computeVigenciaStats(nodes) {
        var total = 0, vigentes = 0, expiradas = 0;
        function walk(arr) {
            if (!arr) return;
            for (var i = 0; i < arr.length; i++) {
                var n = arr[i];
                if (!n) continue;
                total++;
                if (n.esVigente === true) vigentes++;
                else expiradas++;
                walk(n.children || []);
            }
        }
        walk(nodes);
        return { total: total, vigentes: vigentes, expiradas: expiradas };
    }

/**
     * Aplica los switches de filtro al árbol pre-cargado. Devuelve un
     * árbol NUEVO (no muta `nodes`) para que los toggles del usuario
     * puedan dispararse varias veces seguidas sin arrastrar estado
     * entre renders.
     *
     * Reglas (issue #286 — segundo feedback):
     *  - `showExpiradas === true` (switch ON, default) → conservar
     *    TODO el árbol, vigentes y expiradas.
     *  - `showExpiradas === false` (switch OFF) → descartar los nodos
     *    con `esVigente === false` (expiradas) Y TODA su sub-jerarquía,
     *    para evitar huérfanos sin padre visible.
     *
     * Los nodos cuyo `esVigente` no esté definido (null/undefined) se
     * tratan como no vigentes para que el filtro del usuario tenga
     * semántica consistente aunque el servidor no haya proyectado el
     * flag (defensa contra regresiones del wire contract).
     */
    function applyFilters(nodes) {
        if (!nodes) return [];
        var result = [];
        for (var i = 0; i < nodes.length; i++) {
            var node = nodes[i];
            if (!node) {
                continue;
            }

            // Coerción explícita a boolean: cualquier valor !== true
            // (false, null, undefined) cuenta como expirado.
            var esVigente = node.esVigente === true;
            var isExpirada = !esVigente;

            // El switch OFF descarta las expiradas. Si el switch está
            // ON, todas pasan, sin importar la vigencia.
            var shouldHide = !options.showExpiradas && isExpirada;
            if (shouldHide) {
                continue;
            }

            var filteredChildren = applyFilters(node.children || []);
            // Copia superficial para no mutar la entrada; preserva los
            // campos que el JS necesita (id, codigo, nombre, tipo,
            // children, esVigente) y descarta el resto del viewmodel.
            result.push({
                id: node.id,
                codigo: node.codigo,
                nombre: node.nombre,
                tipo: node.tipo,
                esVigente: esVigente,
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
                // del filtro actual. Diagnóstico en consola: el operador
                // puede ver cuántos nodos llegaron y cuántos quedaron
                // después del filtro para entender qué pasó (issue #286).
                var stats = computeVigenciaStats(treeData);
                console.warn(
                    '[OrgChart] Filtro dejó el árbol vacío. ' +
                    'total=' + stats.total + ', vigentes=' + stats.vigentes +
                    ', expiradas=' + stats.expiradas +
                    ', showExpiradas=' + options.showExpiradas
                );
                chartDiv.innerHTML = '<div class="text-center text-muted py-5"><p>No hay unidades organizativas para mostrar con el filtro actual.</p></div>';
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
     * Descarga el chart actual como PNG.
     *
     * Implementación (issue #286 — segundo feedback del operador):
     * NO usamos `currentChart.getImageURI()` de la API de Google Charts
     * porque en `OrgChart` específicamente ese método no está disponible
     * en algunas versiones del loader y el navegador lanza
     * `TypeError: currentChart.getImageURI is not a function` al hacer
     * clic en el botón.
     *
     * La fix robusta es independiente de la API del chart: Google Charts
     * renderiza cada gráfico como un `<svg>` dentro del contenedor, así
     * que serializamos el SVG directamente del DOM, lo cargamos en un
     * `<img>`, lo dibujamos sobre un `<canvas>` con fondo blanco, y
     * exportamos el canvas como PNG. Esto captura exactamente lo que el
     * usuario ve en pantalla (incluyendo colapsados manuales).
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

        // Google Charts inserta el SVG en chartDiv. Lo buscamos ahí en
        // vez de depender del método getImageURI() de la API del chart.
        var svgEl = chartDiv.querySelector('svg');
        if (!svgEl) {
            console.warn('[OrgChart] exportPng: no se encontró <svg> dentro del chart.');
            return;
        }

        var bbox = svgEl.getBoundingClientRect();
        var width = Math.max(1, Math.ceil(bbox.width || svgEl.clientWidth || 800));
        var height = Math.max(1, Math.ceil(bbox.height || svgEl.clientHeight || 600));

        // Serializamos con XMLSerializer. Forzamos el namespace xmlns
        // porque algunos browsers lo pierden al re-crear el árbol SVG
        // y eso rompe la carga en <img>.
        var xml = new XMLSerializer().serializeToString(svgEl);
        if (xml.indexOf('xmlns=') < 0) {
            xml = xml.replace('<svg', '<svg xmlns="http://www.w3.org/2000/svg"');
        }

        var svgBlob = new Blob([xml], { type: 'image/svg+xml;charset=utf-8' });
        var svgUrl = URL.createObjectURL(svgBlob);

        var img = new Image();
        img.onload = function () {
            var canvas = document.createElement('canvas');
            canvas.width = width;
            canvas.height = height;
            var ctx = canvas.getContext('2d');
            // Fondo blanco explícito: el SVG es transparente por default y
            // eso produce un PNG con fondo transparente que muchos visores
            // muestran como negro.
            ctx.fillStyle = '#ffffff';
            ctx.fillRect(0, 0, width, height);
            ctx.drawImage(img, 0, 0, width, height);

            canvas.toBlob(function (pngBlob) {
                URL.revokeObjectURL(svgUrl);
                if (!pngBlob) {
                    console.warn('[OrgChart] exportPng: canvas.toBlob devolvió null.');
                    return;
                }

                var pngUrl = URL.createObjectURL(pngBlob);
                var now = new Date();
                var yyyymmdd = now.getFullYear().toString()
                    + String(now.getMonth() + 1).padStart(2, '0')
                    + String(now.getDate()).padStart(2, '0');
                var a = document.createElement('a');
                a.href = pngUrl;
                a.download = 'organigrama-' + yyyymmdd + '.png';
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
                // Liberamos la URL en el siguiente tick para que el click
                // ya haya consumido el blob antes de invalidarlo.
                setTimeout(function () { URL.revokeObjectURL(pngUrl); }, 0);
            }, 'image/png');
        };
        img.onerror = function () {
            console.warn('[OrgChart] exportPng: error al cargar SVG en <img>.');
            URL.revokeObjectURL(svgUrl);
        };
        img.src = svgUrl;
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