(function () {
  'use strict';
  var API = 'https://api.github.com/repos/nathantek/aion2assistant/releases?per_page=10';
  var RELEASES = 'https://github.com/nathantek/aion2assistant/releases';

  function all(sel) { return Array.prototype.slice.call(document.querySelectorAll(sel)); }

  function el(tag, cls, text) {
    var e = document.createElement(tag);
    if (cls) { e.className = cls; }
    if (text !== undefined) { e.textContent = text; }
    return e;
  }

  function frDate(iso) {
    var d = new Date(iso);
    if (isNaN(d.getTime())) { return ''; }
    return d.toLocaleDateString('fr-FR', { day: 'numeric', month: 'long', year: 'numeric' });
  }

  function isHttps(url) {
    return typeof url === 'string' && url.indexOf('https://github.com/') === 0;
  }

  function setDownloads(href, label) {
    all('[data-download]').forEach(function (b) { b.href = href; b.textContent = label; });
  }
  function setMeta(text) {
    all('[data-download-meta]').forEach(function (m) { m.textContent = text; });
  }
  function setVersion(text) {
    all('[data-version]').forEach(function (v) { v.textContent = text; });
  }

  function fallbackLink(parent, text) {
    var p = el('p', null, text + ' ');
    var a = el('a', null, 'Voir les versions sur GitHub');
    a.href = RELEASES;
    p.appendChild(a);
    p.appendChild(document.createTextNode('.'));
    parent.appendChild(p);
  }

  // Nom affiché d'une version : l'étiquette, sauf une étiquette provisoire « untagged-… » (on prend alors le nom).
  function label(rel) {
    var tag = typeof rel.tag_name === 'string' ? rel.tag_name : '';
    if (tag && tag.indexOf('untagged-') !== 0) { return tag; }
    return typeof rel.name === 'string' && rel.name ? rel.name : tag;
  }

  function findAsset(rel, pattern) {
    var found = null;
    (rel.assets || []).forEach(function (a) {
      if (!found && typeof a.name === 'string' && pattern.test(a.name)) { found = a; }
    });
    return found;
  }

  function fillDownload(releases) {
    var rel = releases[0];
    // Installateur depuis la 0.3.4, ancien zip pour les versions précédentes.
    var asset = findAsset(rel, /-win-Setup\.exe$/) || findAsset(rel, /-win-x64\.zip$/);
    var href = asset && isHttps(asset.browser_download_url) ? asset.browser_download_url
      : (isHttps(rel.html_url) ? rel.html_url : RELEASES);
    var tag = label(rel);
    setDownloads(href, tag ? 'Télécharger ' + tag : 'Télécharger');
    var d = frDate(rel.published_at || rel.created_at);
    setMeta(tag + (d ? ' · publiée le ' + d : '') + (asset ? '' : ' · page de la version'));
    if (tag) { setVersion(tag); }
  }

  function releaseTitle(rel, parent) {
    parent.appendChild(document.createTextNode(rel.name || rel.tag_name || 'Version'));
    if (rel.prerelease) {
      parent.appendChild(document.createTextNode(' '));
      parent.appendChild(el('span', 'badge', 'bêta'));
    }
  }

  function releaseMeta(rel) {
    var d = frDate(rel.published_at || rel.created_at);
    return label(rel) + (d ? ' · ' + d : '');
  }

  function releaseContent(rel, parent) {
    parent.appendChild(el('div', 'release-body',
      rel.body && rel.body.trim() ? rel.body : 'Pas de notes pour cette version.'));
    if (isHttps(rel.html_url)) {
      var p = el('p');
      var a = el('a', null, 'Voir sur GitHub');
      a.href = rel.html_url;
      p.appendChild(a);
      parent.appendChild(p);
    }
  }

  function fillNotes(releases) {
    var list = document.getElementById('releases');
    if (!list) { return; }
    list.textContent = '';
    releases.forEach(function (rel, i) {
      var box;
      if (i === 0) {
        box = el('article', 'release release-latest');
        box.appendChild(el('p', 'release-label', 'Dernière version'));
        var h = el('h2');
        releaseTitle(rel, h);
        box.appendChild(h);
        box.appendChild(el('p', 'release-meta', releaseMeta(rel)));
      } else {
        box = el('details', 'release');
        var s = el('summary');
        var name = el('span', 'release-name');
        releaseTitle(rel, name);
        s.appendChild(name);
        s.appendChild(el('span', 'release-meta', releaseMeta(rel)));
        box.appendChild(s);
      }
      releaseContent(rel, box);
      list.appendChild(box);
    });
  }

  function degrade(metaText, listText) {
    setDownloads(RELEASES, 'Voir les versions sur GitHub');
    setMeta(metaText);
    var list = document.getElementById('releases');
    if (list) { list.textContent = ''; fallbackLink(list, listText); }
  }

  var FAILED_META = 'Impossible de récupérer la dernière version automatiquement.';
  var FAILED_LIST = 'Impossible de charger les versions.';
  var EMPTY = "Aucune version publiée pour l'instant.";

  if (!document.querySelector('[data-download]') && !document.getElementById('releases')) { return; }
  if (typeof fetch !== 'function') { degrade(FAILED_META, FAILED_LIST); return; }

  fetch(API, { headers: { Accept: 'application/vnd.github+json' } })
    .then(function (r) { if (!r.ok) { throw new Error('http ' + r.status); } return r.json(); })
    .then(function (data) {
      if (!Array.isArray(data)) { throw new Error('format'); }
      var releases = data.filter(function (r) { return r && !r.draft; });
      if (!releases.length) { degrade(EMPTY, EMPTY); return; }
      // L'API trie par date de création : on veut la dernière publiée en premier.
      releases.sort(function (a, b) {
        return (Date.parse(b.published_at || b.created_at) || 0) - (Date.parse(a.published_at || a.created_at) || 0);
      });
      fillDownload(releases);
      fillNotes(releases);
    })
    .catch(function () { degrade(FAILED_META, FAILED_LIST); });
})();
