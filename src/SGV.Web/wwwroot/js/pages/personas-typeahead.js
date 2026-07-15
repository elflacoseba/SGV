function readPersonaTypeaheadData(root) {
    var script = root.querySelector('[data-persona-typeahead-data]');
    if (!script) {
        return [];
    }

    try {
        var parsed = JSON.parse(script.textContent || '[]');
        return Array.isArray(parsed) ? parsed : [];
    } catch (err) {
        return [];
    }
}

function matchesPersonaQuery(persona, normalizedQuery) {
    if (!normalizedQuery) {
        return false;
    }

    var legajo = (persona.legajo || '').toLowerCase();
    var apellidos = (persona.apellidos || '').toLowerCase();
    var nombres = (persona.nombres || '').toLowerCase();
    var email = (persona.email || '').toLowerCase();
    var documento = (persona.documento || '').toLowerCase();

    return legajo.indexOf(normalizedQuery) !== -1
        || apellidos.indexOf(normalizedQuery) !== -1
        || nombres.indexOf(normalizedQuery) !== -1
        || email.indexOf(normalizedQuery) !== -1
        || documento.indexOf(normalizedQuery) !== -1;
}

function renderPersonaTypeaheadResults(root, matches) {
    var list = root.querySelector('[data-persona-typeahead-results]');
    var emptyHint = root.querySelector('[data-persona-typeahead-empty]');
    if (!list) {
        return;
    }

    list.innerHTML = '';

    if (matches.length === 0) {
        list.classList.add('d-none');
        if (emptyHint) {
            emptyHint.classList.remove('d-none');
        }
        return;
    }

    if (emptyHint) {
        emptyHint.classList.add('d-none');
    }

    matches.forEach(function (persona) {
        var li = document.createElement('li');
        li.className = 'list-group-item persona-typeahead-item';
        li.setAttribute('data-persona-id', persona.id);
        li.setAttribute('data-persona-display', persona.apellidos + ', ' + persona.nombres +
            (persona.legajo ? ' (Legajo ' + persona.legajo + ')' : ''));
        li.setAttribute('role', 'button');
        li.textContent = persona.apellidos + ', ' + persona.nombres +
            (persona.legajo ? ' · Legajo ' + persona.legajo : '') +
            (persona.email ? ' · ' + persona.email : '');
        list.appendChild(li);
    });

    list.classList.remove('d-none');
}

function firePersonaTypeaheadChange(root, selectedId, display) {
    var hidden = root.querySelector('[data-persona-typeahead-hidden]');
    var container = root;
    if (hidden) {
        hidden.value = selectedId || '';
    }
    container.setAttribute('data-persona-typeahead-selected-id', selectedId || '');

    if (hidden) {
        hidden.dispatchEvent(new Event('change', { bubbles: true }));
    }
}

function wirePersonaTypeahead(root) {
    if (!root) {
        return;
    }

    var data = readPersonaTypeaheadData(root);
    var minChars = parseInt(root.getAttribute('data-min-chars') || '2', 10);
    var input = root.querySelector('[data-persona-typeahead-input]');
    var results = root.querySelector('[data-persona-typeahead-results]');
    var hint = root.querySelector('[data-persona-typeahead-hint]');
    var emptyHint = root.querySelector('[data-persona-typeahead-empty]');
    var debounceMs = 250;
    var debounceTimer = null;

    if (!input || !results) {
        return;
    }

    function update() {
        var query = (input.value || '').trim().toLowerCase();

        if (query.length < minChars) {
            results.classList.add('d-none');
            results.innerHTML = '';
            if (emptyHint) {
                emptyHint.classList.add('d-none');
            }
            if (hint) {
                hint.classList.remove('d-none');
            }
            firePersonaTypeaheadChange(root, '', '');
            return;
        }

        if (hint) {
            hint.classList.add('d-none');
        }

        var matches = data.filter(function (p) {
            return matchesPersonaQuery(p, query);
        }).slice(0, 25);

        renderPersonaTypeaheadResults(root, matches);
    }

    input.addEventListener('input', function () {
        if (debounceTimer) {
            clearTimeout(debounceTimer);
        }
        debounceTimer = setTimeout(update, debounceMs);
    });

    input.addEventListener('focus', function () {
        if (input.value && input.value.trim().length >= minChars) {
            update();
        }
    });

    results.addEventListener('click', function (event) {
        var target = event.target;
        if (!(target instanceof HTMLElement)) {
            return;
        }
        var item = target.closest('[data-persona-id]');
        if (!item) {
            return;
        }
        var id = item.getAttribute('data-persona-id') || '';
        var display = item.getAttribute('data-persona-display') || '';
        input.value = display;
        results.classList.add('d-none');
        results.innerHTML = '';
        if (emptyHint) {
            emptyHint.classList.add('d-none');
        }
        firePersonaTypeaheadChange(root, id, display);
    });

    document.addEventListener('click', function (event) {
        var target = event.target;
        if (!(target instanceof HTMLElement)) {
            return;
        }
        if (!root.contains(target)) {
            results.classList.add('d-none');
        }
    });
}

function wireAllPersonaTypeaheads(root) {
    if (!root || typeof root.querySelectorAll !== 'function') {
        return;
    }

    var nodes = root.querySelectorAll('[data-persona-typeahead]');
    for (var i = 0; i < nodes.length; i++) {
        wirePersonaTypeahead(nodes[i]);
    }
}

if (typeof window !== 'undefined') {
    window.wirePersonaTypeahead = wirePersonaTypeahead;
    window.wireAllPersonaTypeaheads = wireAllPersonaTypeaheads;

    if (window.document) {
        wireAllPersonaTypeaheads(window.document);
    }
}

if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        wirePersonaTypeahead: wirePersonaTypeahead,
        wireAllPersonaTypeaheads: wireAllPersonaTypeaheads,
        readPersonaTypeaheadData: readPersonaTypeaheadData,
        matchesPersonaQuery: matchesPersonaQuery
    };
}