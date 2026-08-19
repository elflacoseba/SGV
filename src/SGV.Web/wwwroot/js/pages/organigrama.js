// Organigrama page — loads the org chart via Google Charts.
// Issue #286: filtros visuales (mostrar código, mostrar expiradas)
// y exportación a PDF vía window.print(). El PNG y el panel de
// diagnóstico se removieron porque el operador prefiere simplicidad
// sobre features que no usa activamente.
(function () {
    'use strict';

    var chartDiv = document.getElementById('orgchart');
    if (!chartDiv) return;

    var showCodeInput = document.getElementById('toggle-show-code');
    var showExpiradasInput = document.getElementById('toggle-show-expiradas');
    var exportPdfBtn = document.getElementById('btn-export-pdf');

    var options = {
        showCode: !showCodeInput || showCodeInput.checked === true,
        showExpiradas: !showExpiradasInput || showExpiradasInput.checked === true
    };

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
     * Determina si una unidad está "expirada".
     * Expirada ÚNICAMENTE cuando VigenteHasta está definido Y es una
     * fecha válida anterior a hoy. Todo lo demás → vigente.
     */
    function isExpired(vigenteDesde, vigenteHasta) {
        var hoy = new Date();
        hoy.setHours(0, 0, 0, 0);
        if (vigenteHasta && typeof vigenteHasta === 'string') {
            var hastaDate = new Date(vigenteHasta + 'T00:00:00');
            if (!isNaN(hastaDate.getTime()) && hastaDate < hoy) {
                return true;
            }
        }
        return false;
    }

    /**
     * Filtra el árbol para ocultar las unidades expiradas (excepto
     * raíces, que siempre se muestran para no dejar el árbol vacío).
     */
    function applyFilters(nodes, isTopLevel) {
        isTopLevel = isTopLevel === true;
        if (!nodes) return [];
        var result = [];
        for (var i = 0; i < nodes.length; i++) {
            var node = nodes[i];
            if (!node) continue;
            var expired = isExpired(node.vigenteDesde, node.vigenteHasta);
            var shouldHide = !options.showExpiradas && expired && !isTopLevel;
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

    /**
     * Escapa markup HTML para que strings provistos por el backend
     * (codigo/nombre/tipo de unidades organizativas) no se inyecten
     * como HTML cuando OrgChart se dibuja con allowHtml:true.
     * Issue W-1 (housekeeping release-readiness UO+Organigrama).
     */
    function escapeHtml(value) {
        if (value === null || value === undefined) return '';
        return String(value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function drawOrgChart() {
        clearTimeout(timeoutId);

        try {
            var treeData = window.__sgvTreeData || [];

            if (!treeData || treeData.length === 0) {
                chartDiv.innerHTML = '<div class="text-center text-muted py-5"><p>No hay unidades organizativas para mostrar en el organigrama.</p></div>';
                currentChart = null;
                chartReady = false;
                return;
            }

            var filtered = applyFilters(treeData, true);
            if (filtered.length === 0) {
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
                    // W-1 (housekeeping release-readiness): OrgChart se dibuja
                    // con allowHtml:true, por lo que cualquier nombre o código
                    // que contenga markup se inyecta como HTML. Escapamos acá
                    // para que un nombre como "<img src=x onerror=...>" se
                    // renderice como texto literal y no como nodo DOM.
                    var safeCodigo = escapeHtml(node.codigo || '');
                    var safeNombre = escapeHtml(node.nombre || '');
                    var safeTipo = escapeHtml(node.tipo || '');
                    var displayName = options.showCode
                        ? safeCodigo + ' \u2014 ' + safeNombre
                        : safeNombre;
                    var tooltip = safeCodigo + ' \u00B7 ' + safeTipo;
                    data.addRow([{ v: nodeId, f: displayName }, parentId ? String(parentId) : '', tooltip]);
                    if (node.children && node.children.length > 0) {
                        flattenTree(node.children, nodeId);
                    }
                }
            }

            flattenTree(filtered, null);

            chartReady = false;
            currentChart = new google.visualization.OrgChart(chartDiv);

            google.visualization.events.addListener(currentChart, 'ready', function () {
                chartReady = true;
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

    /**
     * Dispara el diálogo nativo de impresión del navegador. El usuario
     * elige "Guardar como PDF" en el diálogo.
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