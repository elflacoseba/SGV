// Organigrama page — loads the org chart via Google Charts
(function () {
    'use strict';

    var chartDiv = document.getElementById('orgchart');
    if (!chartDiv) return;

    // Timeout: if Google Charts doesn't load within 10 seconds, show error
    var timeoutId = setTimeout(function () {
        console.error('[OrgChart] Timeout: Google Charts no cargó en 10 segundos');
        if (chartDiv) {
            chartDiv.innerHTML = '<div class="text-center text-muted py-5"><p>No se pudo cargar el organigrama (timeout de Google Charts).</p></div>';
        }
    }, 10000);

    function drawChartWhenReady() {
        clearTimeout(timeoutId);
        drawOrgChart();
    }

    // Check if google namespace is available
    if (typeof google === 'undefined') {
        console.error('[OrgChart] ERROR: google is undefined. El CDN de Google Charts no cargó.');
        chartDiv.innerHTML = '<div class="text-center text-muted py-5"><p>No se pudo cargar el organigrama (CDN de Google Charts no disponible).</p></div>';
        return;
    }

    // Google Charts load
    google.charts.load('current', { packages: ['orgchart'], callback: drawChartWhenReady, errorCallback: function (err) {
        clearTimeout(timeoutId);
        console.error('[OrgChart] ERROR: google.charts.load failed:', err);
        chartDiv.innerHTML = '<div class="text-center text-muted py-5"><p>No se pudo cargar el organigrama (error al cargar Google Charts).</p></div>';
    } });
})();

async function drawOrgChart() {
    var chartDiv = document.getElementById('orgchart');
    if (!chartDiv) return;

    console.log('[OrgChart] drawOrgChart invoked');

    try {
        // El organigrama se hidrata desde datos pre-cargados server-side
        // (ver Organigrama.cshtml: window.__sgvTreeData). Pegar a la API
        // desde el browser daría 401 porque el JWT vive en la cookie
        // httpOnly y ApiBearerTokenHandler solo aplica del lado servidor.
        var treeData = window.__sgvTreeData || [];
        console.log('[OrgChart] Tree data received:', treeData, 'count:', treeData.length);

        if (!treeData || treeData.length === 0) {
            console.warn('[OrgChart] Empty tree data');
            chartDiv.innerHTML = '<div class="text-center text-muted py-5"><p>No hay unidades organizativas para mostrar en el organigrama.</p></div>';
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
                var displayName = node.codigo + ' \u2014 ' + node.nombre;
                var tooltip = node.codigo + ' \u00B7 ' + node.tipo;
                data.addRow([{ v: nodeId, f: displayName }, parentId ? String(parentId) : '', tooltip]);
                if (node.children && node.children.length > 0) {
                    flattenTree(node.children, nodeId);
                }
            }
        }

        flattenTree(treeData, null);
        console.log('[OrgChart] Data table created, rows:', data.getNumberOfRows());

        var chart = new google.visualization.OrgChart(chartDiv);
        chart.draw(data, {
            allowHtml: true,
            allowCollapse: true,
            size: 'medium'
        });
        console.log('[OrgChart] Chart drawn successfully');
    } catch (err) {
        console.error('[OrgChart] ERROR:', err);
        chartDiv.innerHTML = '<div class="text-center text-muted py-5"><p>No se pudo cargar el organigrama. Revisa la consola para más detalles.</p></div>';
    }
}
