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
        // USBJS-02 (revisión #226-followup 2026-07-30): la selección del
        // usuario es siempre válida. El chequeo del contrato ya no aborta
        // el flujo: si los elementos del contrato están presentes (casos
        // 4/5), muta el display; si NO están (caso 6: empty state puro),
        // renderiza una card mínima con Quitar/Cambiar. SIEMPRE cierra el
        // modal, dispara el evento change sobre hiddenInput y habilita el
        // submit (si existe) — la persona queda seleccionada y el
        // PageModel se entera vía Input.PersonaId al guardar.
        hiddenInput.value = persona.id;
        modal.dataset.currentPersonaId = persona.id;

        if (displayInput && cardText && card && empty) {
            displayInput.value = text;
            cardText.textContent = text;
            card.hidden = false;
            empty.hidden = true;
        } else {
            // Caso 6: render dinámico de card mínima en JS.
            console.warn(
                '[usuario-persona-buscador] choose() en empty case: render dinámico. '
                + 'modalId=' + modal.id + ', displayContainerId=' + modal.dataset.displayContainerId
            );
            renderDynamicCard(text);
        }

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

    // USBJS-02 (render dinámico caso 6): cuando la partial no emite la
    // card (Caso 6: editable + PersonaDto null + sin FallbackDisplay), el
    // JS construye una card mínima con texto + Quitar + Cambiar dentro
    // del contenedor display. Replica visualmente el Caso 5 (fallback
    // card) sin requerir recargar la página ni fetch del DTO.
    function renderDynamicCard(text) {
        if (!display) {
            return;
        }

        // Limpiar contenido previo del display (incluye el contenedor vacío
        // emitido por la partial en el Caso 6).
        display.replaceChildren();

        // Wrapper de card.
        var cardEl = root.createElement('div');
        cardEl.className = 'card border mb-0';
        cardEl.setAttribute('data-usuario-persona-card', '');

        var cardBody = root.createElement('div');
        cardBody.className = 'card-body d-flex flex-wrap justify-content-between align-items-center gap-3 py-2';

        // Texto visible de la persona seleccionada.
        var textEl = root.createElement('span');
        textEl.setAttribute('data-usuario-persona-display-text', '');
        textEl.textContent = text;
        cardBody.appendChild(textEl);

        // Botones Quitar / Cambiar.
        var buttonsDiv = root.createElement('div');
        buttonsDiv.className = 'd-flex gap-2';

        var quitarBtn = root.createElement('button');
        quitarBtn.type = 'button';
        quitarBtn.className = 'btn btn-sm btn-outline-danger';
        quitarBtn.setAttribute('data-usuario-persona-quitar', '');
        quitarBtn.textContent = 'Quitar';
        quitarBtn.addEventListener('click', handleQuitar);
        buttonsDiv.appendChild(quitarBtn);

        var cambiarBtn = root.createElement('button');
        cambiarBtn.type = 'button';
        cambiarBtn.className = 'btn btn-sm btn-outline-primary';
        cambiarBtn.setAttribute('data-usuario-persona-buscar', '');
        cambiarBtn.setAttribute('data-bs-toggle', 'modal');
        cambiarBtn.setAttribute('data-bs-target', '#' + modal.id);
        cambiarBtn.textContent = 'Cambiar';
        buttonsDiv.appendChild(cambiarBtn);

        cardBody.appendChild(buttonsDiv);
        cardEl.appendChild(cardBody);
        display.appendChild(cardEl);

        // Hidden input que el JS sincroniza con el display (mismo nombre
        // y atributo que emite la partial en el Caso 5, así la próxima
        // invocación de choose() con persona nueva encuentra el contrato
        // completo y entra al camino de mutación normal).
        var hidden = root.createElement('input');
        hidden.type = 'hidden';
        hidden.name = 'PersonaDisplay';
        hidden.setAttribute('data-usuario-persona-display-input', '');
        hidden.value = text;
        display.appendChild(hidden);

        // Ocultar el empty state para que la card quede como única
        // presentación visible.
        if (empty) {
            empty.hidden = true;
        }
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

    // USBJS-03 (revisión #226-followup 2026-07-30): el handler Quitar
    // ahora es una función nombrada para poder reusarla desde el render
    // dinámico del Caso 6 (los botones Quitar que crea renderDynamicCard
    // bindean este mismo handler). SIEMPRE limpia hiddenInput +
    // currentPersonaId + emite change. El camino de presentación depende
    // del contrato disponible: caso 4/5 muta el display existente; caso 6
    // limpia el render dinámico y muestra el empty state.
    function handleQuitar() {
        hiddenInput.value = '';
        modal.dataset.currentPersonaId = '';

        if (displayInput && cardText && card && empty) {
            // Caso 4/5: comportamiento original.
            displayInput.value = '';
            cardText.textContent = '';
            card.hidden = true;
            empty.hidden = false;
        } else {
            // Caso 6: limpiar render dinámico y volver al empty state.
            if (display) {
                display.replaceChildren();
            }
            if (empty) {
                empty.hidden = false;
            }
        }

        if (submit) {
            submit.disabled = true;
        }
        hiddenInput.dispatchEvent(new Event('change', { bubbles: true }));
    }

    root.querySelectorAll('[data-usuario-persona-quitar]').forEach(function (button) {
        button.addEventListener('click', handleQuitar);
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
