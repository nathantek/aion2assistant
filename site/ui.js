(function () {
  'use strict';

  document.documentElement.classList.add('js');

  function toArray(list) { return Array.prototype.slice.call(list); }

  // Menu mobile
  var toggle = document.querySelector('.nav-toggle');
  var nav = document.getElementById('site-nav');
  if (toggle && nav) {
    var setOpen = function (open) {
      toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
      nav.classList.toggle('is-open', open);
    };
    toggle.addEventListener('click', function () {
      setOpen(toggle.getAttribute('aria-expanded') !== 'true');
    });
    nav.addEventListener('click', function (e) {
      if (e.target.closest('a')) { setOpen(false); }
    });
    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && toggle.getAttribute('aria-expanded') === 'true') {
        setOpen(false);
        toggle.focus();
      }
    });
  }

  // Puces de sections : surligne la section visible
  var chips = document.querySelector('.chips');
  if (chips && 'IntersectionObserver' in window) {
    var row = chips.querySelector('ul');
    var links = toArray(chips.querySelectorAll('a[href^="#"]'));
    var linkById = {};
    var targets = [];
    links.forEach(function (a) {
      var t = document.getElementById(a.getAttribute('href').slice(1));
      if (t) { linkById[t.id] = a; targets.push(t); }
    });
    var activate = function (id) {
      links.forEach(function (a) {
        var on = linkById[id] === a;
        a.classList.toggle('is-active', on);
        if (on) { a.setAttribute('aria-current', 'true'); } else { a.removeAttribute('aria-current'); }
      });
      var cur = linkById[id];
      if (cur && row && row.scrollWidth > row.clientWidth) {
        row.scrollLeft = cur.offsetLeft - row.offsetLeft - (row.clientWidth - cur.offsetWidth) / 2;
      }
    };
    var visible = {};
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (en) { visible[en.target.id] = en.isIntersecting; });
      for (var i = 0; i < targets.length; i++) {
        if (visible[targets[i].id]) { activate(targets[i].id); return; }
      }
    }, { rootMargin: '-130px 0px -55% 0px' });
    targets.forEach(function (t) { io.observe(t); });
  }

  // Onglets de l'accueil (motif ARIA tabs)
  var tablist = document.querySelector('[role="tablist"]');
  if (tablist) {
    var tabs = toArray(tablist.querySelectorAll('[role="tab"]'));
    var panels = tabs.map(function (t) { return document.getElementById(t.getAttribute('aria-controls')); });
    var select = function (index, focus) {
      tabs.forEach(function (t, j) {
        var on = j === index;
        t.setAttribute('aria-selected', on ? 'true' : 'false');
        t.tabIndex = on ? 0 : -1;
        if (panels[j]) { panels[j].hidden = !on; }
      });
      if (focus) { tabs[index].focus(); }
    };
    tabs.forEach(function (t, i) {
      t.addEventListener('click', function () { select(i, false); });
      t.addEventListener('keydown', function (e) {
        var n = tabs.length;
        var j = -1;
        if (e.key === 'ArrowRight') { j = (i + 1) % n; }
        else if (e.key === 'ArrowLeft') { j = (i - 1 + n) % n; }
        else if (e.key === 'Home') { j = 0; }
        else if (e.key === 'End') { j = n - 1; }
        if (j >= 0) { e.preventDefault(); select(j, true); }
      });
    });
    tablist.hidden = false;
    select(0, false);
  }

  // FAQ : ouvre la question visée par l'ancre
  var openFromHash = function () {
    var id = window.location.hash.slice(1);
    if (!id) { return; }
    var el;
    try { el = document.getElementById(decodeURIComponent(id)); } catch (e) { return; }
    if (el && el.tagName === 'DETAILS') { el.open = true; }
  };
  openFromHash();
  window.addEventListener('hashchange', openFromHash);
})();
