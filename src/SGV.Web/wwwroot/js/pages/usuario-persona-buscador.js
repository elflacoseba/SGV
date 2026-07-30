(function (root) {
    'use strict';

    var modal = root.querySelector('[data-usuario-persona-modal]');
    if (!modal || typeof window.bootstrap === 'undefined') {
        return;
    }

    // REQ-USB-12 / OCC-PER-BUSC-03: el filtro `soloSinUsuario` se deriva
    // del atributo `data-solo-sin-usuario` del modal root. Default `true`
    // preserva el back-compat estricto con Usuarios (issue #216).
    // Valores ausentes, no-parseables o distintos de `"false"` caen al
    // default `true`.
    var rawSoloSinUsuario = modal.getAttribute('data-solo-sin-usuario');
    var soloSinUsuarioValue = typeof rawSoloSinUsuario === 'string'
        && rawSoloSinUsuario.toLowerCase() === 'false'
        ? 'false'
        : 'true';

    var searchInput = modal.querySelector('[data-usuario-persona-search]');
    var searchButton = modal.querySelector('[data-usuario-persona-search-button]');
    var rows = modal.querySelector('[data-usuario-persona-rows]');
    var results = modal.querySelector('[data-usuario-persona-results]');
    var pagination = modal.querySelector('[data-usuario-persona-pagination]');
    var previous = modal.querySelector('[data-usuario-persona-previous]');
    var next = modal.querySelector('[data-usuario-persona-next]');
    var hiddenInput = root.getElementById(modal.dataset.hiddenInputId);
    var display = root.getElementById(modal.dataset.displayContainerId);
    var displayInput = display && display.parentElement.querySelector('[data-usuario-persona-display-input]');
    var card = display && display.querySelector('[data-usuario-persona-card]');
    var cardText = display && display.querySelector('[data-usuario-persona-display-text]');
    var empty = display && display.parentElement.querySelector('[data-usuario-persona-empty]');
    var submit = root.querySelector('[data-usuario-persona-submit]');
    var debounceTimer;
    var lastTrigger;
    var currentFetchController;

    function showState(name) {
        modal.querySelectorAll('[data-usuario-persona-status] > div').forEach(function (state) {
            state.hidden = state.id !== 'estado-' + name;
        });
        results.hidden = name !== 'results';
        searchButton.disabled = name === 'loading';
    }

    function personaDisplay(persona) {
        var fullName = [persona.apellidos, persona.nombres].filter(Boolean).join(', ');
        var detail = persona.tipoDocumento && persona.numeroDocumento
            ? persona.tipoDocumento + ': ' + persona.numeroDocumento
            : persona.legajo || 'Sin documento ni legajo';
        return fullName + ' (' + detail + ')';
    }

    function choose(persona) {
        var text = personaDisplay(persona);
        // USBJS-02: actualizar hiddenInput y currentPersonaId siempre (la
        // selección del usuario es válida aunque el display no se pueda
        // sincronizar). Solo el bloque de mutación del display es abortable.
        hiddenInput.value = persona.id;
        modal.dataset.currentPersonaId = persona.id;

        if (!displayInput || !cardText || !card || !empty) {
            console.warn(
                '[usuario-persona-buscador] choose() aborted: missing card contract elements. '
                + 'modalId=' + modal.id + ', displayContainerId=' + modal.dataset.displayContainerId
            );
            return;
        }

        displayInput.value = text;
        cardText.textContent = text;
        card.hidden = false;
        empty.hidden = true;
        if (submit) {
            submit.disabled = false;
        }
        hiddenInput.dispatchEvent(new Event('change', { bubbles: true }));
        if (currentFetchController) {
            currentFetchController.abort();
            currentFetchController = null;
        }
        window.bootstrap.Modal.getOrCreateInstance(modal).hide();
    }

    function appendCell(row, value) {
        var cell = root.createElement('td');
        cell.textContent = value || '—';
        row.appendChild(cell);
    }

    function renderRows(items) {
        rows.replaceChildren();
        items.forEach(function (persona) {
            var row = root.createElement('tr');
            appendCell(row, [persona.apellidos, persona.nombres].filter(Boolean).join(', '));
            appendCell(row, persona.tipoDocumento && persona.numeroDocumento
                ? persona.tipoDocumento + ': ' + persona.numeroDocumento
                : null);
            appendCell(row, persona.legajo);
            appendCell(row, persona.email);

            var action = root.createElement('td');
            action.className = 'text-end';
            var button = root.createElement('button');
            button.type = 'button';
            button.className = 'btn btn-sm btn-primary';
            button.textContent = 'Seleccionar';
            button.setAttribute('aria-label', 'Seleccionar a ' + persona.apellidos + ', ' + persona.nombres);
            button.addEventListener('click', function () { choose(persona); });
            action.appendChild(button);
            row.appendChild(action);
            rows.appendChild(row);
        });
    }

    function pageNumbers(current, total) {
        if (total <= 7) {
            return Array.from({ length: total }, function (_, index) { return index + 1; });
        }
        var selected = new Set([1, total]);
        for (var page = Math.max(2, current - 2); page <= Math.min(total - 1, current + 2); page++) {
            selected.add(page);
        }
        var numbers = Array.from(selected).sort(function (left, right) { return left - right; });
        var tokens = [];
        numbers.forEach(function (number, index) {
            if (index && number - numbers[index - 1] > 1) {
                tokens.push('…');
            }
            tokens.push(number);
        });
        return tokens;
    }

    function renderPagination(current, total) {
        pagination.querySelectorAll('[data-usuario-persona-page]').forEach(function (item) { item.remove(); });
        pageNumbers(current, total).forEach(function (token) {
            var item = root.createElement('li');
            item.className = 'page-item' + (token === current ? ' active' : '');
            item.dataset.usuarioPersonaPage = '';
            var button = root.createElement('button');
            button.type = 'button';
            button.className = 'page-link';
            button.textContent = token;
            button.disabled = token === '…';
            if (typeof token === 'number') {
                button.setAttribute('aria-label', 'Página ' + token);
                button.addEventListener('click', function () { search(token); });
            }
            item.appendChild(button);
            pagination.insertBefore(item, next);
        });
        previous.classList.toggle('disabled', current <= 1);
        next.classList.toggle('disabled', current >= total);
        previous.querySelector('button').disabled = current <= 1;
        next.querySelector('button').disabled = current >= total;
        previous.querySelector('button').onclick = function () { search(current - 1); };
        next.querySelector('button').onclick = function () { search(current + 1); };
    }

    async function search(page) {
        var term = searchInput.value.trim();
        if (!term) {
            showState('inicial');
            return;
        }

        if (currentFetchController) {
            currentFetchController.abort();
        }
        currentFetchController = new AbortController();

        showState('loading');
        modal.querySelectorAll('.page-link').forEach(function (button) { button.disabled = true; });
        var url = new URL(modal.dataset.apiUrl, window.location.origin);
        url.searchParams.set('search', term);
        url.searchParams.set('soloSinUsuario', soloSinUsuarioValue);
        url.searchParams.set('p', page);
        url.searchParams.set('pageSize', '25');

        try {
            var response = await window.fetch(url, {
                headers: { Accept: 'application/json' },
                signal: currentFetchController.signal
            });
            if (!response.ok) {
                throw new Error('HTTP ' + response.status);
            }
            var payload = await response.json();
            currentFetchController = null;
            var items = (payload.items || []).filter(function (persona) {
                return persona.id !== modal.dataset.currentPersonaId;
            });
            if (!items.length) {
                showState('empty');
                return;
            }
            renderRows(items);
            renderPagination(payload.page || page, Math.max(1, Math.ceil(payload.totalCount / 25)));
            showState('results');
        } catch (error) {
            if (error && error.name === 'AbortError') {
                return;
            }
            currentFetchController = null;
            showState('error');
        }
    }

    searchInput.addEventListener('input', function () {
        window.clearTimeout(debounceTimer);
        if (!searchInput.value.trim()) {
            showState('inicial');
            return;
        }
        debounceTimer = window.setTimeout(function () { search(1); }, 300);
    });
    searchInput.addEventListener('keydown', function (event) {
        if (event.key === 'Enter') {
            event.preventDefault();
            window.clearTimeout(debounceTimer);
            search(1);
        }
    });
    searchButton.addEventListener('click', function () { search(1); });

    root.querySelectorAll('[data-usuario-persona-quitar]').forEach(function (button) {
        button.addEventListener('click', function () {
            // USBJS-03: limpiar hiddenInput y currentPersonaId siempre;
            // abortar mutaciones del display si falta algún elemento del contrato.
            hiddenInput.value = '';
            modal.dataset.currentPersonaId = '';

            if (!displayInput || !cardText || !card || !empty) {
                console.warn(
                    '[usuario-persona-buscador] Quitar aborted: missing card contract elements. '
                    + 'modalId=' + modal.id + ', displayContainerId=' + modal.dataset.displayContainerId
                );
                return;
            }

            displayInput.value = '';
            cardText.textContent = '';
            card.hidden = true;
            empty.hidden = false;
            if (submit) {
                submit.disabled = true;
            }
            hiddenInput.dispatchEvent(new Event('change', { bubbles: true }));
        });
    });

    modal.addEventListener('show.bs.modal', function (event) { lastTrigger = event.relatedTarget; });
    modal.addEventListener('shown.bs.modal', function () { searchInput.focus(); });
    modal.addEventListener('hidden.bs.modal', function () {
        if (currentFetchController) {
            currentFetchController.abort();
            currentFetchController = null;
        }
        if (lastTrigger) { lastTrigger.focus(); }
    });
})(document);
