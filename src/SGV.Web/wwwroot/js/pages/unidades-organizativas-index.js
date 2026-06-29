function wireUnidadOrganizativaDeleteConfirmation(root, swal) {
    if (!root || !swal || typeof swal.fire !== 'function') {
        return;
    }

    root.querySelectorAll('[data-uo-delete-form]').forEach(function (form) {
        var button = form.querySelector('[data-uo-delete-button]');
        if (!button) {
            return;
        }

        button.addEventListener('click', function (event) {
            event.preventDefault();

            swal.fire({
                title: '¿Eliminar unidad organizativa?',
                text: 'Se eliminará la unidad organizativa ' + (button.getAttribute('data-uo-item-name') || '') + ' (' + (button.getAttribute('data-uo-item-code') || '') + ').',
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: 'Sí, eliminar',
                cancelButtonText: 'Cancelar',
                reverseButtons: true
            }).then(function (result) {
                if (result.isConfirmed) {
                    if (typeof form.requestSubmit === 'function') {
                        form.requestSubmit(button);
                        return;
                    }

                    form.submit();
                }
            });
        });
    });
}

function wireUnidadOrganizativaReactivateConfirmation(root, swal) {
    if (!root || !swal || typeof swal.fire !== 'function') {
        return;
    }

    root.querySelectorAll('[data-uo-reactivate-form]').forEach(function (form) {
        var button = form.querySelector('[data-uo-reactivate-button]');
        if (!button) {
            return;
        }

        button.addEventListener('click', function (event) {
            event.preventDefault();

            swal.fire({
                title: '¿Reactivar unidad organizativa?',
                text: 'Se reactivará la unidad organizativa ' + (button.getAttribute('data-uo-item-name') || '') + ' (' + (button.getAttribute('data-uo-item-code') || '') + ').',
                icon: 'question',
                showCancelButton: true,
                confirmButtonText: 'Sí, reactivar',
                cancelButtonText: 'Cancelar',
                reverseButtons: true
            }).then(function (result) {
                if (result.isConfirmed) {
                    if (typeof form.requestSubmit === 'function') {
                        form.requestSubmit(button);
                        return;
                    }

                    form.submit();
                }
            });
        });
    });
}

function loadUnidadOrganizativaOrgChart() {
    var chartDiv = document.getElementById('orgchart');
    if (!chartDiv) return;

    // Timeout: if Google Charts doesn't load within 10 seconds, show error
    var timeoutId = setTimeout(function() {
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
    google.charts.load('current', {packages:['orgchart'], callback: drawChartWhenReady, errorCallback: function(err) {
        clearTimeout(timeoutId);
        console.error('[OrgChart] ERROR: google.charts.load failed:', err);
        chartDiv.innerHTML = '<div class="text-center text-muted py-5"><p>No se pudo cargar el organigrama (error al cargar Google Charts).</p></div>';
    }});
}

async function drawOrgChart() {
    var chartDiv = document.getElementById('orgchart');
    if (!chartDiv) return;

    console.log('[OrgChart] drawOrgChart invoked');

    try {
        // API base URL set by the Razor page from appsettings
        var apiBase = window.__sgvApiBaseUrl || '';
        console.log('[OrgChart] API base URL:', apiBase);
        console.log('[OrgChart] Fetching tree data from ' + apiBase + '/api/v1/unidades-organizativas/arbol');
        var response = await fetch(apiBase + '/api/v1/unidades-organizativas/arbol');
        console.log('[OrgChart] Response status:', response.status);
        if (!response.ok) throw new Error('HTTP ' + response.status);
        var treeData = await response.json();
        console.log('[OrgChart] Tree data received:', treeData, 'count:', treeData ? treeData.length : 0);

        if (!treeData || treeData.length === 0) {
            console.warn('[OrgChart] API returned empty tree data');
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
                var tooltip = node.codigo + ' \u00B7 ' + node.tipoUnidadNombre;
                data.addRow([{v: nodeId, f: displayName}, parentId ? String(parentId) : '', tooltip]);
                if (node.hijas && node.hijas.length > 0) {
                    flattenTree(node.hijas, nodeId);
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

if (typeof window !== 'undefined') {
    window.wireUnidadOrganizativaDeleteConfirmation = wireUnidadOrganizativaDeleteConfirmation;
    window.wireUnidadOrganizativaReactivateConfirmation = wireUnidadOrganizativaReactivateConfirmation;

    if (window.document && window.Swal) {
        wireUnidadOrganizativaDeleteConfirmation(window.document, window.Swal);
        wireUnidadOrganizativaReactivateConfirmation(window.document, window.Swal);
        loadUnidadOrganizativaOrgChart();
    }
}

if (typeof module !== 'undefined' && module.exports) {
    module.exports = { wireUnidadOrganizativaDeleteConfirmation };
}
