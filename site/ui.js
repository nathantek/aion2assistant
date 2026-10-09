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

  // Puces de sections : surligne la section en cours de lecture
  var chips = document.querySelector('.chips');
  if (chips) {
    var row = chips.querySelector('ul');
    var items = [];
    toArray(chips.querySelectorAll('a[href^="#"]')).forEach(function (a) {
      var t = document.getElementById(a.getAttribute('href').slice(1));
      if (t) { items.push({ link: a, target: t }); }
    });
    var current = null;
    var activate = function (item) {
      if (item === current) { return; }
      current = item;
      items.forEach(function (it) {
        var on = it === item;
        it.link.classList.toggle('is-active', on);
        if (on) { it.link.setAttribute('aria-current', 'location'); } else { it.link.removeAttribute('aria-current'); }
      });
      if (item && row && row.scrollWidth > row.clientWidth) {
        row.scrollLeft = item.link.offsetLeft - row.offsetLeft - (row.clientWidth - item.link.offsetWidth) / 2;
      }
    };
    var update = function () {
      if (!items.length) { return; }
      var line = chips.getBoundingClientRect().bottom + 32;
      var doc = document.documentElement;
      var found = items[0];
      if (window.innerHeight + window.pageYOffset >= doc.scrollHeight - 2) {
        found = items[items.length - 1];
      } else {
        for (var i = 0; i < items.length; i++) {
          if (items[i].target.getBoundingClientRect().top <= line) { found = items[i]; } else { break; }
        }
      }
      activate(found);
    };
    var ticking = false;
    window.addEventListener('scroll', function () {
      if (ticking) { return; }
      ticking = true;
      window.requestAnimationFrame(function () { ticking = false; update(); });
    }, { passive: true });
    window.addEventListener('resize', update);
    update();
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
