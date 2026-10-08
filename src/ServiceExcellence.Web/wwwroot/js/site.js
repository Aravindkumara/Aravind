// Cascading drop-downs for the product classification (division -> category -> model -> variant -> assembly).
//
// <select data-cascade-parent="#DivisionId" data-cascade-url="/lookup/masters/Category" data-cascade-param="parentId"
//         data-selected="3" data-placeholder="All categories">
//
// When the parent changes, the child is reloaded and its own change event fires so the whole chain refreshes.
(function () {
    'use strict';

    function populate(select, items, selected) {
        var placeholder = select.dataset.placeholder || '-- Select --';
        select.innerHTML = '';
        select.appendChild(new Option(placeholder, ''));
        items.forEach(function (item) {
            var text = item.path
                ? '  '.repeat((item.level || 1) - 1) + item.name + ' (' + item.code + ')'
                : item.name + ' (' + item.code + ')';
            var option = new Option(text, item.id);
            if (String(item.id) === String(selected)) option.selected = true;
            select.appendChild(option);
        });
        select.disabled = false;
    }

    function load(select, selected) {
        var parent = document.querySelector(select.dataset.cascadeParent);
        var parentValue = parent ? parent.value : '';
        if (!parentValue) {
            populate(select, [], null);
            select.disabled = true;
            select.dispatchEvent(new Event('change'));
            return Promise.resolve();
        }

        var url = select.dataset.cascadeUrl + '?' + (select.dataset.cascadeParam || 'parentId') + '=' + encodeURIComponent(parentValue);
        select.disabled = true;
        return fetch(url, { headers: { 'Accept': 'application/json' }, credentials: 'same-origin' })
            .then(function (r) {
                if (!r.ok) throw new Error('Lookup failed: ' + r.status);
                return r.json();
            })
            .then(function (items) {
                populate(select, items, selected);
                select.dispatchEvent(new Event('change'));
            })
            .catch(function (err) {
                console.error(err);
                populate(select, [], null);
            });
    }

    document.addEventListener('DOMContentLoaded', function () {
        var selects = Array.prototype.slice.call(document.querySelectorAll('select[data-cascade-parent]'));
        // Selects whose initial value has not yet been applied; the first load of each uses data-selected.
        var pending = new Set(selects);

        selects.forEach(function (select) {
            var parent = document.querySelector(select.dataset.cascadeParent);
            if (!parent) return;
            parent.addEventListener('change', function () {
                var selected = pending.has(select) ? select.dataset.selected : null;
                pending.delete(select);
                load(select, selected);
            });
        });

        // Kick off the chain from the roots (selects whose parent is not itself cascading).
        selects.filter(function (s) {
            var parent = document.querySelector(s.dataset.cascadeParent);
            return parent && !parent.dataset.cascadeParent;
        }).forEach(function (s) {
            var selected = s.dataset.selected;
            pending.delete(s);
            load(s, selected);
        });

        // Light / dark theme toggle; the choice is remembered per browser.
        document.querySelectorAll('[data-theme-toggle]').forEach(function (button) {
            button.addEventListener('click', function () {
                var next = document.documentElement.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark';
                document.documentElement.setAttribute('data-bs-theme', next);
                try { localStorage.setItem('se-theme', next); } catch (e) { }
            });
        });

        // Bootstrap tooltips: <button title="..." data-bs-toggle="tooltip">
        if (window.bootstrap && bootstrap.Tooltip) {
            document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(function (el) { new bootstrap.Tooltip(el); });
        }

        // Confirmation prompts for destructive actions: <form data-confirm="Are you sure?">
        document.querySelectorAll('form[data-confirm]').forEach(function (form) {
            form.addEventListener('submit', function (e) {
                if (!window.confirm(form.dataset.confirm)) e.preventDefault();
            });
        });
    });
})();
