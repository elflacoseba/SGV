// Organigrama page — loads the org chart via Google Charts
// Issue #286 (6to round): el botón Exportar PNG reescrito tras
// revisar la documentación oficial de Google Charts. La doc confirma
// que `OrgChart` NO expone `getImageURI()` en su lista de métodos, y
// que ese método "actualmente funciona para gráficos principales y
// geográficos" — OrgChart NO está incluido. Por eso las rondas
// anteriores fallaban.
//
// Estrategia (post-investigación oficial):
//   1. Esperar al evento `ready` del chart antes de habilitar export
//      (es el patrón documentado para llamadas a métodos después de
//      draw).
//   2. Capturar el `<svg>` que Google Charts renderiza (la doc dice:
//      "Los gráficos se renderizan con la tecnología de HTML5/SVG").
//   3. Rasterizar via Canvas con xmlns/xlink/viewBox explícitos y
//      crossOrigin anonymous para evitar tainted canvas.
//   4. Si canvas falla → descarga SVG directa.
//   5. Si descarga falla → abre nueva ventana con el SVG.
//   6. Si todo falla → `chart.print()` documentado oficialmente.
(function () {
    'use strict';

    var chartDiv = document.getElementById('orgchart');
    if (!chartDiv) return;

    var showCodeInput = document.getElementById('toggle-show-code');
    var showExpiradasInput = document.getElementById('toggle-show-expiradas');
    var exportPngBtn = document.getElementById('btn-export-png');
    var exportPdfBtn = document.getElementById('btn-export-pdf');
    var diagPanel = document.getElementById('orgchart-diag');

    var options = {
        showCode: !showCodeInput || showCodeInput.checked === true,
        showExpiradas: !showExpiradasInput || showExpiradasInput.checked === true
    };

    // Referencia al chart activo. Solo se asigna después del evento
    // `ready` del chart (patrón documentado oficialmente). Hasta
    // entonces, los exports retornan con un warning.
    var currentChart = null;
    var chartReady = false;

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
     * Determina si una unidad está "expirada" (issue #286 4to round).
     * Expirada ÚNICAMENTE cuando VigenteHasta está definido Y es una
     * fecha válida anterior a hoy. Todo lo demás → vigente.
     */
    function isExpired(vigenteDesde, vigenteHasta) {
        var hoy = new Date();
        hoy.setHours(0, 0, 0, 0);
        if (vigenteHasta && typeof vigenteHasta === 'string') {
            var hastaDate = new Date(vigenteHasta + 'T00:00:00');
            if (!isNaN(hastaDate.getTime()) && hastaDate < hoy) {
                return { expired: true, reason: 'vigenteHasta < hoy' };
            }
        }
        return { expired: false, reason: classifyVigente(vigenteDesde, vigenteHasta) };
    }

    /**
     * Aplica diferenciación visual a los nodos según su estado de vigencia.
     *
     * Issue #286 (noveno feedback): los nodos se veían todos con el mismo
     * fondo (texto cortado y sin diferenciación de expiradas). El default
     * de Google Charts aplica algunos estilos inconsistentes (gris oscuro
     * a VIC_ACA/VIC_ADM/SRV_BIB sin razón aparente).
     *
     * Estrategia: en el evento `ready`, iteramos todos los
     * `.google-visualization-orgchart-node` y comparamos su texto (que es
     * "CODIGO — Nombre" porque `allowHtml: true`) con los códigos de las
     * unidades. Si está expirada, agregamos la clase `orgchart-node-expired`
     * que aplica el fondo oscuro con texto claro. Si no, fondo blanco con
     * texto oscuro.
     */
    function applyExpirationStyling(nodes) {
        var expiredByCodigo = {};
        function walk(arr) {
            for (var i = 0; i < arr.length; i++) {
                var n = arr[i];
                var r = isExpired(n.vigenteDesde, n.vigenteHasta);
                if (r.expired) {
                    expiredByCodigo[n.codigo] = n;
                }
                walk(n.children || []);
            }
        }
        walk(nodes);

        var nodeEls = chartDiv.querySelectorAll('.google-visualization-orgchart-node');
        var matched = 0;
        for (var i = 0; i < nodeEls.length; i++) {
            var el = nodeEls[i];
            // El texto del nodo es "CODIGO — Nombre" (modo showCode=true)
            // o solo "Nombre" (modo showCode=false). En el primer caso
            // extraemos el código; en el segundo, no podemos mapear al
            // nodo original así que no aplicamos estilo especial.
            var text = (el.textContent || '').trim();
            var dashIdx = text.indexOf('—');
            var codigo = dashIdx > 0 ? text.substring(0, dashIdx).trim() : null;
            if (codigo && expiredByCodigo[codigo]) {
                el.classList.add('orgchart-node-expired');
                matched++;
            } else {
                el.classList.remove('orgchart-node-expired');
            }
        }
        console.log('[OrgChart] applyExpirationStyling: matched', matched, 'of', nodeEls.length, 'nodes');
    }

    function classifyVigente(vigenteDesde, vigenteHasta) {
        var hoy = new Date();
        hoy.setHours(0, 0, 0, 0);
        if (!vigenteHasta && !vigenteDesde) return 'sin ventana';
        if (vigenteHasta) {
            var h = new Date(vigenteHasta + 'T00:00:00');
            if (!isNaN(h.getTime()) && h >= hoy) return 'vigenteHasta ≥ hoy';
        }
        if (vigenteDesde) {
            var d = new Date(vigenteDesde + 'T00:00:00');
            if (!isNaN(d.getTime()) && d > hoy) return 'pendiente inicio (desde > hoy)';
        }
        return 'vigente';
    }

    function computeVigenciaStats(nodes) {
        var total = 0, vigentes = 0, expiradas = 0, detalle = [];
        function walk(arr, parent, isRoot) {
            if (!arr) return;
            for (var i = 0; i < arr.length; i++) {
                var n = arr[i];
                if (!n) continue;
                total++;
                var r = isExpired(n.vigenteDesde, n.vigenteHasta);
                if (r.expired) expiradas++;
                else vigentes++;
                detalle.push({
                    codigo: n.codigo,
                    nombre: n.nombre,
                    vigenteDesde: n.vigenteDesde || '—',
                    vigenteHasta: n.vigenteHasta || '—',
                    estado: r.expired ? 'expirada' : 'vigente',
                    motivo: r.reason,
                    padre: parent,
                    esRaiz: isRoot === true
                });
                walk(n.children || [], n.codigo, false);
            }
        }
        walk(nodes, null, true);
        return { total: total, vigentes: vigentes, expiradas: expiradas, detalle: detalle };
    }

    function applyFilters(nodes, isTopLevel) {
        isTopLevel = isTopLevel === true;
        if (!nodes) return [];
        var result = [];
        for (var i = 0; i < nodes.length; i++) {
            var node = nodes[i];
            if (!node) continue;
            var r = isExpired(node.vigenteDesde, node.vigenteHasta);
            var shouldHide = !options.showExpiradas && r.expired && !isTopLevel;
            if (shouldHide) continue;
            var filteredChildren = applyFilters(node.children || [], false);
            result.push({
                id: node.id,
                codigo: node.codigo,
                nombre: node.nombre,
                tipo: node.tipo,
                vigenteDesde: node.vigenteDesde,
                vigenteHasta: node.vigenteHasta,
                children: filteredChildren
            });
        }
        return result;
    }

    function renderDiagPanel(stats) {
        if (!diagPanel) return;
        var rows = stats.detalle.map(function (d) {
            var badge = d.estado === 'expirada'
                ? '<span class="badge bg-danger">expirada</span>'
                : '<span class="badge bg-success">vigente</span>';
            var rootMark = d.esRaiz
                ? ' <span class="badge bg-secondary ms-1" title="Nodo raíz: siempre visible aunque esté expirado">raíz</span>'
                : '';
            return '<tr>'
                + '<td><code>' + escapeHtml(d.codigo) + '</code></td>'
                + '<td>' + escapeHtml(d.nombre) + '</td>'
                + '<td>' + escapeHtml(String(d.vigenteDesde)) + '</td>'
                + '<td>' + escapeHtml(String(d.vigenteHasta)) + '</td>'
                + '<td>' + badge + rootMark + '</td>'
                + '<td><small class="text-muted">' + escapeHtml(d.motivo) + '</small></td>'
                + '</tr>';
        }).join('');

        diagPanel.innerHTML =
            '<div class="card border-info mt-3">'
            + '<div class="card-header bg-info-subtle"><strong>Diagnóstico de vigencia</strong></div>'
            + '<div class="card-body">'
            + '<p class="mb-2">Total: <strong>' + stats.total + '</strong> · Vigentes: <strong>' + stats.vigentes + '</strong> · Expiradas: <strong>' + stats.expiradas + '</strong> · Switch "Mostrar expiradas": <strong>' + (options.showExpiradas ? 'ON (muestra todas)' : 'OFF (oculta expiradas)') + '</strong></p>'
            + '<p class="mb-2 small text-muted"><i class="mdi mdi-information-outline me-1"></i>Los nodos marcados como <span class="badge bg-secondary">raíz</span> son las entradas top-level del árbol y siempre se muestran, incluso con el switch en OFF. Esto evita que el organigrama quede completamente vacío si la raíz tiene un VigenteHasta en el pasado por error de datos. Si ves una raíz marcada como "expirada", revisá su fecha de cierre en la BD.</p>'
            + '<div class="table-responsive"><table class="table table-sm table-bordered mb-0"><thead><tr><th>Código</th><th>Nombre</th><th>Vigente desde</th><th>Vigente hasta</th><th>Estado</th><th>Motivo</th></tr></thead><tbody>'
            + rows
            + '</tbody></table></div>'
            + '</div></div>';
    }

    function escapeHtml(s) {
        return String(s).replace(/[&<>"']/g, function (c) {
            return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c];
        });
    }

    function drawOrgChart() {
        clearTimeout(timeoutId);

        try {
            var treeData = window.__sgvTreeData || [];

            if (!treeData || treeData.length === 0) {
                chartDiv.innerHTML = '<div class="text-center text-muted py-5"><p>No hay unidades organizativas para mostrar en el organigrama.</p></div>';
                currentChart = null;
                chartReady = false;
                if (diagPanel) diagPanel.innerHTML = '';
                return;
            }

            renderDiagPanel(computeVigenciaStats(treeData));

            var filtered = applyFilters(treeData, true);
            if (filtered.length === 0) {
                var stats = computeVigenciaStats(treeData);
                console.warn(
                    '[OrgChart] Filtro dejó el árbol vacío. ' +
                    'total=' + stats.total + ', vigentes=' + stats.vigentes +
                    ', expiradas=' + stats.expiradas +
                    ', showExpiradas=' + options.showExpiradas
                );
                chartDiv.innerHTML = '<div class="text-center text-muted py-5"><p>No hay unidades organizativas para mostrar con el filtro actual.</p></div>';
                currentChart = null;
                chartReady = false;
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

            chartReady = false;
            currentChart = new google.visualization.OrgChart(chartDiv);

            // Patrón documentado oficialmente: escuchar el evento
            // `ready` antes de llamar a métodos sobre el chart.
            // Sin esto, el chart podría no estar completamente
            // renderizado cuando intentemos exportar.
            google.visualization.events.addListener(currentChart, 'ready', function () {
                chartReady = true;
                applyExpirationStyling(filtered);
                console.log('[OrgChart] chart ready, OK para export.');
            });

            currentChart.draw(data, {
                allowHtml: true,
                allowCollapse: true,
                size: 'medium'
            });
        } catch (err) {
            console.error('[OrgChart] ERROR:', err);
            chartDiv.innerHTML = '<div class="text-center text-muted py-5"><p>No se pudo cargar el organigrama. Revisa la consola para más detalles.</p></div>';
            currentChart = null;
            chartReady = false;
        }
    }

    function getDateStamp() {
        var now = new Date();
        return now.getFullYear().toString()
            + String(now.getMonth() + 1).padStart(2, '0')
            + String(now.getDate()).padStart(2, '0');
    }

    function downloadBlob(blob, filename) {
        var url = URL.createObjectURL(blob);
        var a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(function () { URL.revokeObjectURL(url); }, 0);
    }

    /**
     * Exporta el chart actual como PNG.
     *
     * Cadena de fallback (issue #286 — séptimo feedback):
     *
     *  1. **PNG via `<svg>` → canvas**: el approach original. Funciona
     *     para charts que renderizan a SVG (la mayoría de Google Charts
     *     menos OrgChart).
     *
     *  2. **PNG via html2canvas** (issue #286 — séptimo feedback):
     *     captura el chartDiv completo como canvas. Funciona para
     *     CUALQUIER elemento DOM, incluyendo la `<table>` que OrgChart
     *     usa como tecnología de render. Cargada via CDN en el cshtml.
     *
     *  3. **window.print()**: función global del navegador. Abre el
     *     diálogo de impresión con @media print ya configurado para
     *     ocultar la toolbar y mostrar solo el chart. El usuario
     *     elige "Guardar como PDF" en el diálogo.
     *
     * Esta función ya NO usa `chart.print()` ni `getImageURI()` — la
     * doc oficial de OrgChart confirma que ninguno existe para este
     * chart type.
     */
    function exportPng() {
        if (!currentChart) {
            console.warn('[OrgChart] exportPng: chart no inicializado.');
            return;
        }
        if (!chartReady) {
            console.warn('[OrgChart] exportPng: chart aún no ready, intentando de todas formas...');
        }

        // Paso 1: probar el approach SVG (rápido, sin dependencia).
        var svgEl = chartDiv.querySelector('svg');
        if (svgEl) {
            rasterizeSvgToPng(svgEl);
            return;
        }

        // Paso 2: html2canvas captura el chartDiv completo (tabla, divs,
        // lo que sea). Esto maneja el caso real de OrgChart que usa
        // <table> y no <svg>.
        if (typeof html2canvas === 'function') {
            console.log('[OrgChart] exportPng: no hay <svg>, intentando con html2canvas (OrgChart usa <table>).');
            rasterizeWithHtml2Canvas();
            return;
        }

        // Paso 3: fallback final.
        console.warn('[OrgChart] exportPng: ni <svg> ni html2canvas disponibles. Fallback final → window.print().');
        window.print();
    }

    /**
     * Rasteriza el `<svg>` a PNG via Canvas + toBlob. Usado para
     * charts de Google Charts que sí renderizan a SVG.
     */
    function rasterizeSvgToPng(svgEl) {
        var viewBox = svgEl.viewBox && svgEl.viewBox.baseVal;
        var bbox = svgEl.getBoundingClientRect();
        var width = Math.max(800, Math.ceil(
            (viewBox && viewBox.width) || bbox.width || svgEl.clientWidth || 800
        ));
        var height = Math.max(600, Math.ceil(
            (viewBox && viewBox.height) || bbox.height || svgEl.clientHeight || 600
        ));

        console.log('[OrgChart] exportPng: SVG bounds', { w: width, h: height, viewBox: !!viewBox });

        var clonedSvg = svgEl.cloneNode(true);
        clonedSvg.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
        clonedSvg.setAttribute('xmlns:xlink', 'http://www.w3.org/1999/xlink');
        clonedSvg.setAttribute('width', String(width));
        clonedSvg.setAttribute('height', String(height));
        if (!clonedSvg.getAttribute('viewBox') && viewBox) {
            clonedSvg.setAttribute('viewBox',
                viewBox.x + ' ' + viewBox.y + ' ' + viewBox.width + ' ' + viewBox.height);
        } else if (!clonedSvg.getAttribute('viewBox')) {
            clonedSvg.setAttribute('viewBox', '0 0 ' + width + ' ' + height);
        }

        var xml = new XMLSerializer().serializeToString(clonedSvg);
        var svgBlob = new Blob([xml], { type: 'image/svg+xml;charset=utf-8' });
        var svgUrl = URL.createObjectURL(svgBlob);

        var img = new Image();
        img.crossOrigin = 'anonymous';
        img.onload = function () {
            try {
                var canvas = document.createElement('canvas');
                canvas.width = width;
                canvas.height = height;
                var ctx = canvas.getContext('2d');
                ctx.fillStyle = '#ffffff';
                ctx.fillRect(0, 0, width, height);
                ctx.drawImage(img, 0, 0, width, height);

                canvas.toBlob(function (blob) {
                    URL.revokeObjectURL(svgUrl);
                    if (!blob) {
                        console.warn('[OrgChart] exportPng: canvas.toBlob null. Fallback a window.print().');
                        window.print();
                        return;
                    }
                    console.log('[OrgChart] exportPng: PNG generado OK, tamaño=', blob.size);
                    downloadBlob(blob, 'organigrama-' + getDateStamp() + '.png');
                }, 'image/png');
            } catch (e) {
                console.warn('[OrgChart] exportPng: error en canvas. Fallback a window.print().', e);
                URL.revokeObjectURL(svgUrl);
                window.print();
            }
        };
        img.onerror = function () {
            console.warn('[OrgChart] exportPng: SVG no cargó en <img>. Fallback a window.print().');
            URL.revokeObjectURL(svgUrl);
            window.print();
        };
        img.src = svgUrl;
    }

    /**
     * Captura el chartDiv completo usando html2canvas. Funciona para
     * CUALQUIER elemento DOM — incluyendo la `<table>` que OrgChart
     * genera. Esta es la fix correcta para el bug reportado por el
     * operador en el séptimo feedback del issue #286.
     */
    function rasterizeWithHtml2Canvas() {
        html2canvas(chartDiv, {
            backgroundColor: '#ffffff',
            scale: window.devicePixelRatio || 1,
            logging: false,
            useCORS: true
        }).then(function (canvas) {
            canvas.toBlob(function (blob) {
                if (!blob) {
                    console.warn('[OrgChart] exportPng: html2canvas.toBlob null. Fallback a window.print().');
                    window.print();
                    return;
                }
                console.log('[OrgChart] exportPng: PNG generado via html2canvas, tamaño=', blob.size);
                downloadBlob(blob, 'organigrama-' + getDateStamp() + '.png');
            }, 'image/png');
        }).catch(function (err) {
            console.error('[OrgChart] exportPng: html2canvas falló. Fallback a window.print().', err);
            window.print();
        });
    }

    /**
     * Dispara el diálogo nativo de impresión del navegador. Documentado
     * oficialmente en la página de "Cómo imprimir archivos PNG". El
     * usuario elige "Guardar como PDF" en el diálogo.
     */
    function exportPdf() {
        window.print();
    }

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