(function () {
  'use strict';
  var API = 'https://api.github.com/repos/nathantek/aion2assistant/releases?per_page=10';
  var RELEASES = 'https://github.com/nathantek/aion2assistant/releases';

  function frDate(iso) {
    var d = new Date(iso);
    if (isNaN(d.getTime())) { return ''; }
    return d.toLocaleDateString('fr-FR', { day: 'numeric', month: 'long', year: 'numeric' });
  }

  function fallbackLink(parent, text) {
    var p = document.createElement('p');
    p.appendChild(document.createTextNode(text + ' '));
    var a = document.createElement('a');
    a.href = RELEASES;
    a.textContent = 'Voir les versions sur GitHub';
    p.appendChild(a);
    p.appendChild(document.createTextNode('.'));
    parent.appendChild(p);
  }

  function isHttps(url) {
    return typeof url === 'string' && url.indexOf('https://github.com/') === 0;
  }

  function fillDownload(releases) {
    var btn = document.getElementById('download-btn');
    var meta = document.getElementById('download-meta');
    if (!btn) { return; }
    var rel = releases[0];
    var asset = null;
    (rel.assets || []).forEach(function (a) {
      if (!asset && typeof a.name === 'string' && /-win-x64\.zip$/.test(a.name)) { asset = a; }
    });
    var href = asset && isHttps(asset.browser_download_url) ? asset.browser_download_url
      : (isHttps(rel.html_url) ? rel.html_url : RELEASES);
    btn.href = href;
    btn.textContent = 'Télécharger ' + (rel.tag_name || '');
    if (meta) {
      var d = frDate(rel.published_at || rel.created_at);
      meta.textContent = (rel.tag_name || '') + (d ? ' · publiée le ' + d : '') +
        (asset ? '' : ' · page de la version');
    }
  }

  function fillNotes(releases) {
    var list = document.getElementById('releases');
    if (!list) { return; }
    list.textContent = '';
    if (!releases.length) { fallbackLink(list, "Aucune version publiée pour l'instant."); return; }
    releases.forEach(function (rel) {
      var art = document.createElement('article');
      art.className = 'release';
      var h = document.createElement('h2');
      h.textContent = rel.name || rel.tag_name || 'Version';
      if (rel.prerelease) {
        h.appendChild(document.createTextNode(' '));
        var b = document.createElement('span');
        b.className = 'badge';
        b.textContent = 'bêta';
        h.appendChild(b);
      }
      art.appendChild(h);
      var t = document.createElement('p');
      t.className = 'muted';
      var d = frDate(rel.published_at || rel.created_at);
      t.textContent = (rel.tag_name || '') + (d ? ' · ' + d : '');
      art.appendChild(t);
      var body = document.createElement('div');
      body.className = 'release-body';
      body.textContent = rel.body && rel.body.trim() ? rel.body : 'Pas de notes pour cette version.';
      art.appendChild(body);
      if (isHttps(rel.html_url)) {
        var p = document.createElement('p');
        var a = document.createElement('a');
        a.href = rel.html_url;
        a.textContent = 'Voir sur GitHub';
        p.appendChild(a);
        art.appendChild(p);
      }
      list.appendChild(art);
    });
  }

  function empty() {
    var btn = document.getElementById('download-btn');
    if (btn) { btn.href = RELEASES; btn.textContent = 'Voir les versions sur GitHub'; }
    var meta = document.getElementById('download-meta');
    if (meta) { meta.textContent = "Aucune version publiée pour l'instant."; }
    var list = document.getElementById('releases');
    if (list) { list.textContent = ''; fallbackLink(list, "Aucune version publiée pour l'instant."); }
  }

  function fail() {
    var btn = document.getElementById('download-btn');
    if (btn) { btn.href = RELEASES; btn.textContent = 'Voir les versions sur GitHub'; }
    var meta = document.getElementById('download-meta');
    if (meta) { meta.textContent = 'Impossible de récupérer la dernière version automatiquement.'; }
    var list = document.getElementById('releases');
    if (list) { list.textContent = ''; fallbackLink(list, 'Impossible de charger les notes de version.'); }
  }

  if (!document.getElementById('download-btn') && !document.getElementById('releases')) { return; }
  if (typeof fetch !== 'function') { fail(); return; }

  fetch(API, { headers: { Accept: 'application/vnd.github+json' } })
    .then(function (r) { if (!r.ok) { throw new Error('http ' + r.status); } return r.json(); })
    .then(function (data) {
      if (!Array.isArray(data)) { throw new Error('format'); }
      var releases = data.filter(function (r) { return r && !r.draft; });
      if (!releases.length) { empty(); return; }
      fillDownload(releases);
      fillNotes(releases);
    })
    .catch(fail);
})();
