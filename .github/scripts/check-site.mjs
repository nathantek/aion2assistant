// Vérifie le site statique (site/) : CSP, absence d'inline, liens, ancres,
// en-tête/pied, scripts, textes bannis, budget polices et CLUF inchangé.
// Usage (depuis la racine du dépôt public) : node .github/scripts/check-site.mjs
import { readFileSync, existsSync, readdirSync, statSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const SITE = join(ROOT, 'site');
const PAGES = ['index', 'guide', 'installation', 'faq', 'notes', 'legal'];
const API_PAGES = new Set(['index', 'installation', 'notes']);
const NAV = ['guide.html', 'installation.html', 'faq.html', 'notes.html'];
const CURRENT = { guide: 'guide.html', installation: 'installation.html', faq: 'faq.html', notes: 'notes.html' };
const CSP = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; connect-src https://api.github.com; object-src 'none'; base-uri 'self'; form-action 'none'";
const LEGAL_REF = '9a2a75a';
const FONT_BUDGET = 112640; // 110 Ko
const BANNED = [
  [/🏆/u, 'emoji trophée'],
  [/#cda9ff/i, 'ancien violet de survol'],
  [/Notes de version/, 'ancien nom de la page Versions'],
];

const failures = [];
const fail = (tag, file, msg) => failures.push(`[${tag}] ${file}: ${msg}`);
const read = (path) => readFileSync(path, 'utf8').replace(/\r\n/g, '\n');
const idsOf = (src) => [...src.matchAll(/\sid="([^"]+)"/g)].map((m) => m[1]);
const between = (src, open, close) => {
  const i = src.indexOf(open);
  if (i < 0) return null;
  const j = src.indexOf(close, i);
  return j < 0 ? null : src.slice(i, j + close.length);
};
const checkBanned = (file, src) => {
  for (const [re, why] of BANNED) if (re.test(src)) fail('banned', file, why);
};

const html = Object.fromEntries(PAGES.map((p) => [p, read(join(SITE, `${p}.html`))]));

for (const p of PAGES) {
  const src = html[p];
  const file = `${p}.html`;

  const csp = src.match(/<meta http-equiv="Content-Security-Policy" content="([^"]*)">/);
  if (!csp) fail('csp', file, 'meta CSP absente');
  else if (csp[1] !== CSP) fail('csp', file, 'CSP différente de la référence');

  if (/\sstyle=/i.test(src)) fail('inline', file, 'attribut style= interdit (CSP)');
  if (/<style[\s>]/i.test(src)) fail('inline', file, 'balise <style> interdite (CSP)');
  if (/\son[a-z]+\s*=/i.test(src)) fail('inline', file, 'gestionnaire on…= interdit (CSP)');
  for (const m of src.matchAll(/<script\b([^>]*)>/gi)) {
    if (!/\ssrc=/.test(m[1])) fail('inline', file, 'script inline interdit (CSP)');
  }

  const ids = idsOf(src);
  for (const id of new Set(ids.filter((id, i) => ids.indexOf(id) !== i))) fail('id', file, `id en double : ${id}`);

  for (const m of src.matchAll(/\s(?:href|src)="([^"]+)"/g)) {
    const url = m[1];
    if (/^(https?:|mailto:)/.test(url)) continue;
    const [path, hash] = url.split('#');
    if (path !== '' && !existsSync(join(SITE, path))) { fail('link', file, `cible introuvable : ${url}`); continue; }
    if (hash) {
      const page = path === '' ? p : path.replace(/\.html$/, '');
      if (!html[page]) fail('anchor', file, `ancre vers une page inconnue : ${url}`);
      else if (!idsOf(html[page]).includes(hash)) fail('anchor', file, `ancre introuvable : ${url}`);
    }
  }

  for (const m of src.matchAll(/<img\b([^>]*)>/gi)) {
    if (!/\salt="/.test(m[1])) fail('img', file, 'img sans alt');
    if (!/\swidth="\d+"/.test(m[1]) || !/\sheight="\d+"/.test(m[1])) fail('img', file, 'img sans width/height');
  }

  const header = between(src, '<header class="site-header">', '</header>');
  if (!header) fail('header', file, 'en-tête .site-header absent');
  else {
    const nav = [...(between(header, '<nav', '</nav>') || '').matchAll(/<a href="([^"]+)"/g)].map((m) => m[1]);
    if (nav.join(',') !== NAV.join(',')) fail('header', file, `navigation attendue ${NAV.join(', ')}, trouvée ${nav.join(', ') || 'aucune'}`);
    if (!header.includes('href="installation.html#telecharger"')) fail('header', file, 'bouton Télécharger de l’en-tête absent');
    if (!header.includes('class="nav-toggle"')) fail('header', file, 'bouton de menu mobile absent');
    const cur = [...header.matchAll(/<a href="([^"]+)" aria-current="page"/g)].map((m) => m[1]);
    const want = CURRENT[p] ? [CURRENT[p]] : [];
    if (cur.join() !== want.join()) fail('current', file, `aria-current attendu sur ${want.join() || 'aucun lien'}, trouvé sur ${cur.join() || 'aucun lien'}`);
  }

  const footer = between(src, '<footer class="site-footer">', '</footer>');
  if (!footer) fail('footer', file, 'pied de page absent');
  else {
    for (const need of ['href="legal.html"', 'href="https://ko-fi.com/nathantek"', 'non affilié à NCSOFT']) {
      if (!footer.includes(need)) fail('footer', file, `pied de page sans ${need}`);
    }
  }

  const scripts = [...src.matchAll(/<script src="([^"]+)"/g)].map((m) => m[1]);
  const wantsApi = API_PAGES.has(p);
  if (!scripts.includes('ui.js')) fail('script', file, 'ui.js manquant');
  if (scripts.includes('app.js') !== wantsApi) fail('script', file, wantsApi ? 'app.js manquant' : 'app.js chargé hors des pages API');
  if (/\sdata-download[\s>=]/.test(src) !== wantsApi) fail('script', file, wantsApi ? 'aucun [data-download]' : '[data-download] hors des pages API');
  if (src.includes('id="download-btn"') || src.includes('id="download-meta"')) fail('script', file, 'ancien id download-btn/download-meta');

  checkBanned(file, src);
}

for (const f of ['style.css', 'app.js', 'ui.js']) {
  if (existsSync(join(SITE, f))) checkBanned(f, read(join(SITE, f)));
}

const css = read(join(SITE, 'style.css'));
if (/@import/.test(css)) fail('css', 'style.css', '@import interdit');
for (const m of css.matchAll(/url\(\s*["']?([^"')]+)["']?\s*\)/g)) {
  const u = m[1];
  if (/^(https?:)?\/\//.test(u)) fail('css', 'style.css', `ressource externe : ${u}`);
  else if (!u.startsWith('data:') && !existsSync(join(SITE, u))) fail('css', 'style.css', `fichier introuvable : ${u}`);
}

const fontsDir = join(SITE, 'assets', 'fonts');
if (!existsSync(fontsDir)) fail('fonts', 'assets/fonts', 'dossier absent');
else {
  const files = readdirSync(fontsDir);
  const total = files.filter((f) => f.endsWith('.woff2')).reduce((n, f) => n + statSync(join(fontsDir, f)).size, 0);
  if (total > FONT_BUDGET) fail('fonts', 'assets/fonts', `${total} octets > budget ${FONT_BUDGET}`);
  for (const lic of ['OFL-SpaceGrotesk.txt', 'OFL-Inter.txt', 'OFL-JetBrainsMono.txt']) {
    if (!files.includes(lic)) fail('fonts', 'assets/fonts', `licence manquante : ${lic}`);
  }
}

const pre = (s) => between(s, '<pre class="legal-text">', '</pre>');
try {
  const ref = execFileSync('git', ['show', `${LEGAL_REF}:site/legal.html`], { cwd: ROOT, encoding: 'utf8' }).replace(/\r\n/g, '\n');
  const now = pre(html.legal);
  if (!now || now !== pre(ref)) fail('legal', 'legal.html', 'le texte du CLUF a changé');
} catch {
  fail('legal', 'legal.html', `impossible de lire la référence git ${LEGAL_REF}`);
}

if (failures.length) {
  console.log(failures.join('\n'));
  console.log(`\n${failures.length} problème(s).`);
  process.exit(1);
}
console.log(`Site OK : ${PAGES.length} pages vérifiées.`);
