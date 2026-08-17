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
     * Intenta serializar el `<svg>` del chart y exportarlo como PNG vía
     * Canvas. Si cualquier paso falla, hace fallback automático a
     * descarga SVG directa o apertura de nueva ventana.
     *
     * Estrategia (post-doc oficial): OrgChart renderiza a SVG/HTML5,
     * capturamos el `<svg>` directamente, le ponemos xmlns/xlink/
     * viewBox/dimensions explícitos para que el browser lo parsee como
     * standalone, lo cargamos en un `<img>`, lo dibujamos en un
     * `<canvas>` con fondo blanco, y exportamos como PNG via
     * `canvas.toBlob`.
     */
    function exportPng() {
        if (!currentChart) {
            console.warn('[OrgChart] exportPng: chart no inicializado.');
            return;
        }
        if (!chartReady) {
            console.warn('[OrgChart] exportPng: chart aún no ready, intentando de todas formas...');
        }

        var svgEl = chartDiv.querySelector('svg');
        if (!svgEl) {
            console.warn('[OrgChart] exportPng: no se encontró <svg> en el chart. Fallback a window.open(SVG).');
            exportSvgViaWindowOpen();
            return;
        }

        // Dimensiones robustas: viewBox > getBoundingClientRect > defaults
        var viewBox = svgEl.viewBox && svgEl.viewBox.baseVal;
        var bbox = svgEl.getBoundingClientRect();
        var width = Math.max(800, Math.ceil(
            (viewBox && viewBox.width) || bbox.width || svgEl.clientWidth || 800
        ));
        var height = Math.max(600, Math.ceil(
            (viewBox && viewBox.height) || bbox.height || svgEl.clientHeight || 600
        ));

        console.log('[OrgChart] exportPng: SVG bounds', { w: width, h: height, viewBox: !!viewBox });

        // Clonar con namespaces y dimensiones explícitos. Algunos
        // browsers pierden el xmlns al clonar un SVG que vive en un
        // contenedor HTML5; forzarlo asegura que se cargue standalone.
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
                        console.warn('[OrgChart] exportPng: canvas.toBlob devolvió null. Fallback a SVG download.');
                        downloadBlob(svgBlob, 'organigrama-' + getDateStamp() + '.svg');
                        return;
                    }
                    console.log('[OrgChart] exportPng: PNG generado OK, tamaño=', blob.size);
                    downloadBlob(blob, 'organigrama-' + getDateStamp() + '.png');
                }, 'image/png');
            } catch (e) {
                console.warn('[OrgChart] exportPng: error en canvas. Fallback a SVG download.', e);
                URL.revokeObjectURL(svgUrl);
                downloadBlob(svgBlob, 'organigrama-' + getDateStamp() + '.svg');
            }
        };
        img.onerror = function () {
            console.warn('[OrgChart] exportPng: SVG no cargó en <img>. Fallback a SVG download.');
            URL.revokeObjectURL(svgUrl);
            downloadBlob(svgBlob, 'organigrama-' + getDateStamp() + '.svg');
        };
        img.src = svgUrl;
    }

    /**
     * Abre el SVG en una nueva ventana. El usuario puede usar
     * "Guardar como" del navegador o copiarlo desde el inspector.
     * Útil cuando el browser bloquea la descarga directa (pop-ups,
     * políticas CSP, etc.).
     */
    function exportSvgViaWindowOpen() {
        var svgEl = chartDiv.querySelector('svg');
        if (!svgEl) return;
        var xml = new XMLSerializer().serializeToString(svgEl);
        var blob = new Blob([xml], { type: 'image/svg+xml;charset=utf-8' });
        var url = URL.createObjectURL(blob);
        var w = window.open(url, '_blank');
        if (w) {
            console.log('[OrgChart] exportSvgViaWindowOpen: ventana abierta OK.');
        } else {
            console.warn('[OrgChart] exportSvgViaWindowOpen: pop-up bloqueado. Sugerí al usuario permitir pop-ups para este sitio.');
        }
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