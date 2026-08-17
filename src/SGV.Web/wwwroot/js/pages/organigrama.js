// Organigrama page — loads the org chart via Google Charts
// Issue #286 (4to round): se corrige la semántica de "expirada" para
// que SOLO considere VigenteHasta en el pasado. Anteriormente también
// marcaba como expiradas las unidades con VigenteDesde en el futuro
// ("aún no empiezan"), lo cual era confuso para el operador. Ahora
// "expirada" = únicamente VigenteHasta configurado y anterior a hoy.
// Todo lo demás (null, futuro, ambos null) se considera vigente.
(function () {
    'use strict';

    var chartDiv = document.getElementById('orgchart');
    if (!chartDiv) return;

    var showCodeInput = document.getElementById('toggle-show-code');
    var showExpiradasInput = document.getElementById('toggle-show-expiradas');
    var exportPngBtn = document.getElementById('btn-export-png');
    var exportPdfBtn = document.getElementById('btn-export-pdf');
    var diagPanel = document.getElementById('orgchart-diag');

    // Estado vivo de los switches. Se inicializa desde los checkboxes
    // (que arrancan `checked` en el HTML) y se mantiene sincronizado
    // con el `change` event.
    var options = {
        showCode: !showCodeInput || showCodeInput.checked === true,
        showExpiradas: !showExpiradasInput || showExpiradasInput.checked === true
    };

    // Referencia al chart activo. La exportacion PNG/PDF lo consume
    // directamente; se asigna en drawOrgChart y queda null cuando no
    // hay árbol renderizado.
    var currentChart = null;

    // Timeout: si Google Charts no carga en 10 segundos, mostramos
    // error. El timeout se cancela apenas el chart se renderiza OK
    // o cuando la la carga falla con errorCallback.
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
     *
     * Una unidad se considera expirada ÚNICAMENTE cuando:
     *  - VigenteHasta está definido Y es una fecha válida Y es
     *    anterior a hoy.
     *
     * Caso contrario (cualquiera de estos) → NO expirada:
     *  - VigenteHasta es null (sin fecha de expiración configurada).
     *  - VigenteHasta es hoy o futuro (sigue vigente).
     *  - VigenteHasta es malformado (no se puede parsear).
     *  - VigenteDesde es futuro (la unidad aún no empieza, pero NO
     *    está "expirada" — está pendiente de inicio).
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
        // NO considerar VigenteDesde futuro como expirada — solo es
        // "pendiente de inicio", sigue siendo una unidad vigente en
        // términos del filtro del operador.
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

    /**
     * Cuenta total / vigentes / expiradas del árbol y lista detalle
     * por nodo para el panel de diagnóstico visible en la página.
     */
    function computeVigenciaStats(nodes) {
        var total = 0, vigentes = 0, expiradas = 0, detalle = [];
        function walk(arr, parent) {
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
                    padre: parent
                });
                walk(n.children || [], n.codigo);
            }
        }
        walk(nodes, null);
        return { total: total, vigentes: vigentes, expiradas: expiradas, detalle: detalle };
    }

    /**
     * Aplica los switches de filtro al árbol pre-cargado.
     *
     * Reglas (issue #286 — 4to round):
     *  - `showExpiradas === true` (switch ON, default) → conservar
     *    TODO el árbol, vigentes y expiradas.
     *  - `showExpiradas === false` (switch OFF) → descartar los nodos
     *    con VigenteHasta en el pasado Y TODA su sub-jerarquía.
     */
    function applyFilters(nodes) {
        if (!nodes) return [];
        var result = [];
        for (var i = 0; i < nodes.length; i++) {
            var node = nodes[i];
            if (!node) continue;

            var r = isExpired(node.vigenteDesde, node.vigenteHasta);
            var shouldHide = !options.showExpiradas && r.expired;
            if (shouldHide) continue;

            var filteredChildren = applyFilters(node.children || []);
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

    /**
     * Renderiza el panel de diagnóstico visible en la página. Muestra
     * los conteos globales y una tabla con cada nodo, su ventana de
     * vigencia y el resultado del filtro. El operador lo ve directo en
     * pantalla sin tener que abrir DevTools.
     */
    function renderDiagPanel(stats) {
        if (!diagPanel) return;
        var rows = stats.detalle.map(function (d) {
            var badge = d.estado === 'expirada'
                ? '<span class="badge bg-danger">expirada</span>'
                : '<span class="badge bg-success">vigente</span>';
            return '<tr>'
                + '<td><code>' + escapeHtml(d.codigo) + '</code></td>'
                + '<td>' + escapeHtml(d.nombre) + '</td>'
                + '<td>' + escapeHtml(String(d.vigenteDesde)) + '</td>'
                + '<td>' + escapeHtml(String(d.vigenteHasta)) + '</td>'
                + '<td>' + badge + '</td>'
                + '<td><small class="text-muted">' + escapeHtml(d.motivo) + '</small></td>'
                + '</tr>';
        }).join('');

        diagPanel.innerHTML =
            '<div class="card border-info mt-3">'
            + '<div class="card-header bg-info-subtle"><strong>Diagnóstico de vigencia</strong></div>'
            + '<div class="card-body">'
            + '<p class="mb-2">Total: <strong>' + stats.total + '</strong> · Vigentes: <strong>' + stats.vigentes + '</strong> · Expiradas: <strong>' + stats.expiradas + '</strong> · Switch "Mostrar expiradas": <strong>' + (options.showExpiradas ? 'ON (muestra todas)' : 'OFF (oculta expiradas)') + '</strong></p>'
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
                if (diagPanel) diagPanel.innerHTML = '';
                return;
            }

            // Renderizar panel de diagnóstico SIEMPRE (antes del filtro).
            // Así el operador ve qué datos llegan y por qué se ocultan.
            renderDiagPanel(computeVigenciaStats(treeData));

            var filtered = applyFilters(treeData);
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
     * Estrategia: capturar el `<svg>` que Google Charts renderiza,
     * serializarlo con XMLSerializer, envolverlo en Blob, cargarlo en un
     * `<img>`, dibujarlo sobre un `<canvas>` con fondo blanco, y exportar
     * el canvas como PNG. Si el canvas se tinta (security) o toBlob
     * falla, hacer fallback a descarga directa del SVG.
     */
    function exportPng() {
        if (!currentChart) {
            console.warn('[OrgChart] exportPng: chart no disponible.');
            return;
        }

        var svgEl = chartDiv.querySelector('svg');
        if (!svgEl) {
            console.warn('[OrgChart] exportPng: no se encontró <svg> dentro del chart.');
            return;
        }

        var bbox = svgEl.getBoundingClientRect();
        var width = Math.max(1, Math.ceil(bbox.width || svgEl.clientWidth || 800));
        var height = Math.max(1, Math.ceil(bbox.height || svgEl.clientHeight || 600));

        var clonedSvg = svgEl.cloneNode(true);
        clonedSvg.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
        clonedSvg.setAttribute('width', String(width));
        clonedSvg.setAttribute('height', String(height));

        var xml = new XMLSerializer().serializeToString(clonedSvg);
        var svgBlob = new Blob([xml], { type: 'image/svg+xml;charset=utf-8' });
        var svgUrl = URL.createObjectURL(svgBlob);

        var img = new Image();
        img.onload = function () {
            try {
                var canvas = document.createElement('canvas');
                canvas.width = width;
                canvas.height = height;
                var ctx = canvas.getContext('2d');
                ctx.fillStyle = '#ffffff';
                ctx.fillRect(0, 0, width, height);
                ctx.drawImage(img, 0, 0, width, height);

                canvas.toBlob(function (pngBlob) {
                    URL.revokeObjectURL(svgUrl);
                    if (!pngBlob) {
                        console.warn('[OrgChart] exportPng: canvas.toBlob devolvió null, fallback a SVG.');
                        downloadAsSvg(xml);
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
                    setTimeout(function () { URL.revokeObjectURL(pngUrl); }, 0);
                }, 'image/png');
            } catch (e) {
                console.warn('[OrgChart] exportPng: error en canvas, fallback a SVG.', e);
                URL.revokeObjectURL(svgUrl);
                downloadAsSvg(xml);
            }
        };
        img.onerror = function () {
            console.warn('[OrgChart] exportPng: error al cargar SVG en <img>, fallback a SVG.');
            URL.revokeObjectURL(svgUrl);
            downloadAsSvg(xml);
        };
        img.src = svgUrl;
    }

    function downloadAsSvg(xml) {
        var blob = new Blob([xml], { type: 'image/svg+xml;charset=utf-8' });
        var url = URL.createObjectURL(blob);
        var now = new Date();
        var yyyymmdd = now.getFullYear().toString()
            + String(now.getMonth() + 1).padStart(2, '0')
            + String(now.getDate()).padStart(2, '0');
        var a = document.createElement('a');
        a.href = url;
        a.download = 'organigrama-' + yyyymmdd + '.svg';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(function () { URL.revokeObjectURL(url); }, 0);
    }

    /**
     * Dispara el diálogo nativo de impresión del navegador. El usuario
     * elige "Guardar como PDF" en el diálogo. La regla `@media print`
     * embebida en `Organigrama.cshtml` ya oculta la toolbar, los
     * switches y el shell visual.
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