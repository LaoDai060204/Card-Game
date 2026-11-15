(function () {
  'use strict';
  const $ = id => document.getElementById(id);
  const assets = window.BS_ASSETS;
  const cards = window.BS_CARDS;
  const phases = ['Start Step', 'Core Step', 'Draw Step', 'Refresh Step', 'Main Step', 'Attack Step', 'Second Main Step', 'End Step'];
  const state = { mode: 'arena', selected: 'ritenia', phase: 4, sound: false, reduced: matchMedia('(prefers-reduced-motion: reduce)').matches, quality: 'high', holograms: true, playing: false, ready: false };
  const stage = $('stage');
  let renderer, scene, camera, board, soloFloor, soloActor, cutscene;
  let frame = 0, last = 0, elapsed = 0, lastDraw = 0, toastTimer, closingCutscene = false;
  let yaw = 0, pitch = .45, zoom = 1, dragging = false, moved = false, pointer = null;
  const actors = [], labels = [], ownedGeometries = new Set(), ownedMaterials = new Set(), ownedTextures = new Set();
  const textures = {};
  const loading = $('loading');
  function notify(message) { $('toast').textContent = message; $('toast').classList.add('on'); clearTimeout(toastTimer); toastTimer = setTimeout(() => $('toast').classList.remove('on'), 2600); }
  function geometry(g) { ownedGeometries.add(g); return g; }
  function material(m) { ownedMaterials.add(m); return m; }
  function mesh(g, m) { return new THREE.Mesh(geometry(g), material(m)); }
  function canvasTexture(w, h, draw) {
    const c = document.createElement('canvas'); c.width = w; c.height = h; draw(c.getContext('2d'), w, h);
    const t = new THREE.CanvasTexture(c); t.encoding = THREE.sRGBEncoding; t.anisotropy = Math.min(8, renderer.capabilities.getMaxAnisotropy()); ownedTextures.add(t); return t;
  }
  function rounded(x, y, w, h, r, ctx) { ctx.beginPath(); ctx.moveTo(x + r, y); ctx.arcTo(x + w, y, x + w, y + h, r); ctx.arcTo(x + w, y + h, x, y + h, r); ctx.arcTo(x, y + h, x, y, r); ctx.arcTo(x, y, x + w, y, r); ctx.closePath(); }
  function boardTexture() {
    return canvasTexture(1776, 1776, (x, w, h) => {
      const s = w / 14.8, P = (a, b) => [(a + 7.4) * s, (b + 7.4) * s];
      let g = x.createRadialGradient(w / 2, h / 2, 10, w / 2, h / 2, w * .72);
      g.addColorStop(0, '#163348'); g.addColorStop(.65, '#0b1b2c'); g.addColorStop(1, '#050d19'); x.fillStyle = g; x.fillRect(0, 0, w, h);
      x.strokeStyle = 'rgba(105,153,183,.07)'; x.lineWidth = 1;
      for (let i = 0; i < w; i += 28) { x.beginPath(); x.moveTo(i, 0); x.lineTo(i - w * .4, h); x.stroke(); }
      const silver = x.createLinearGradient(0, 0, w, h); silver.addColorStop(0, '#d5e0e9'); silver.addColorStop(.3, '#6b879d'); silver.addColorStop(.6, '#d2e6f2'); silver.addColorStop(1, '#436779');
      for (const inset of [23, 34, 48]) { x.lineWidth = inset === 34 ? 4 : 2; x.strokeStyle = silver; rounded(inset, inset, w - 2 * inset, h - 2 * inset, 38, x); x.stroke(); }
      x.lineWidth = 2;
      for (let i = 0; i < 22; i++) {
        const a = 84 + i * (w - 168) / 21;
        for (const y of [34, h - 34]) { x.strokeStyle = 'rgba(193,224,239,.65)'; x.beginPath(); x.ellipse(a, y, 23, 10, i % 2 ? .35 : -.35, 0, Math.PI * 2); x.stroke(); }
      }
      function box(a, b, ww, hh, label, accent) {
        const p = P(a, b); x.fillStyle = 'rgba(3,11,21,.62)'; rounded(p[0] - ww * s / 2, p[1] - hh * s / 2, ww * s, hh * s, 10, x); x.fill();
        x.strokeStyle = accent || 'rgba(146,193,218,.4)'; x.lineWidth = 2; x.stroke(); x.font = '600 23px Arial'; x.textAlign = 'center'; x.fillStyle = '#a6bccb'; x.fillText(label, p[0], p[1] - hh * s / 2 + 30);
      }
      function half() {
        box(-5.7, 3.6, 2.1, 2.05, 'LIFE'); box(-5.7, 5.95, 2.1, 1.9, 'RESERVE');
        box(5.7, 3.6, 2.1, 2.05, 'DECK'); box(5.7, 5.95, 2.1, 1.9, 'TRASH');
        box(0, 4.75, 8.25, 1.55, 'NEXUS', 'rgba(131,176,199,.24)');
        const p = P(0, 1.8); x.strokeStyle = 'rgba(116,194,225,.25)'; x.setLineDash([12, 10]); rounded(p[0] - 4.1 * s, p[1] - 1.75 * s, 8.2 * s, 3.5 * s, 13, x); x.stroke(); x.setLineDash([]);
        x.font = '500 19px Arial'; x.fillStyle = 'rgba(174,210,230,.38)'; x.fillText('SPIRIT FIELD', p[0], p[1] + 1.55 * s);
      }
      half(); x.save(); x.translate(w / 2, h / 2); x.rotate(Math.PI); x.translate(-w / 2, -h / 2); half(); x.restore();
      x.strokeStyle = 'rgba(148,214,244,.4)'; x.lineWidth = 3; x.beginPath(); x.moveTo(70, h / 2); x.lineTo(w - 70, h / 2); x.stroke();
      x.fillStyle = '#0c1c2d'; x.fillRect(w / 2 - 230, h / 2 - 29, 460, 58); x.font = 'italic 600 38px Georgia'; x.fillStyle = '#d3e3ed'; x.textAlign = 'center'; x.fillText('Battle Spirits', w / 2, h / 2 + 12);
    });
  }
  function flatCard(tex, width = 1.05, depth = 1.53) {
    const g = new THREE.Group();
    const base = mesh(new THREE.BoxGeometry(width, .045, depth), new THREE.MeshBasicMaterial({ color: 0x2c3e50 })); base.position.y = .022; g.add(base);
    const face = mesh(new THREE.PlaneGeometry(width * .985, depth * .985), new THREE.MeshBasicMaterial({ map: tex, side: THREE.DoubleSide })); face.rotation.x = -Math.PI / 2; face.position.y = .047; g.add(face); return g;
  }
  function setupBoard() {
    board = new THREE.Group(); scene.add(board);
    const plinth = mesh(new THREE.BoxGeometry(15.05, .18, 15.05), new THREE.MeshBasicMaterial({ color: 0x07111c })); plinth.position.y = -.12; board.add(plinth);
    const surface = mesh(new THREE.PlaneGeometry(14.8, 14.8), new THREE.MeshBasicMaterial({ map: boardTexture() })); surface.rotation.x = -Math.PI / 2; surface.position.y = -.02; board.add(surface);
    for (const sign of [1, -1]) {
      const side = new THREE.Group(); if (sign < 0) side.rotation.y = Math.PI; board.add(side);
      for (let i = 0; i < 5; i++) resource(side, 'core', -6.15 + (i % 3) * .45, 3.5 + Math.floor(i / 3) * .55, .38);
      for (let i = 0; i < 4; i++) resource(side, i === 3 ? 'soul' : 'core', -6.22 + (i % 2) * .7, 5.7 + Math.floor(i / 2) * .55, .43);
      const deck = flatCard(textures.back, 1.07, 1.5); deck.position.set(5.7, .10, 3.6); side.add(deck);
      const deckSide = mesh(new THREE.BoxGeometry(1.07, .18, 1.5), new THREE.MeshBasicMaterial({ color: 0x8391a0 })); deckSide.position.set(5.7, .01, 3.6); side.add(deckSide);
    }
    addActor('ritenia', -2.25, 1.85, false); addActor('pisketona', 2.25, 1.85, false); addActor('pisketona', .25, -2.45, true);
    soloFloor = mesh(new THREE.CircleGeometry(4.8, 80), new THREE.MeshBasicMaterial({ map: canvasTexture(512, 512, (x) => { const g = x.createRadialGradient(256, 256, 15, 256, 256, 256); g.addColorStop(0, 'rgba(20,44,60,.7)'); g.addColorStop(.7, 'rgba(9,21,34,.3)'); g.addColorStop(1, 'rgba(5,11,20,0)'); x.fillStyle = g; x.fillRect(0, 0, 512, 512); }), transparent: true })); soloFloor.rotation.x = -Math.PI / 2; soloFloor.position.y = -.03; soloFloor.visible = false; scene.add(soloFloor);
  }
  function resource(parent, key, x, z, size) { const m = mesh(new THREE.PlaneGeometry(size, size), new THREE.MeshBasicMaterial({ map: textures[key], transparent: true, depthWrite: false })); m.rotation.x = -Math.PI / 2; m.position.set(x, .04, z); parent.add(m); }
  function makeActor(id, height) { return new window.HologramActor({ cardTexture: textures[id + 'Card'], characterTexture: textures[id + 'Cut'], color: cards[id].color, height, cardWidth: 1.35, cardHeight: 1.96, quality: state.quality, reducedMotion: state.reduced }); }
  function addActor(id, x, z, opponent) {
    const actor = makeActor(id, opponent ? 1.95 : 2.5); actor.group.position.set(x, .08, z); if (opponent) actor.group.rotation.y = Math.PI; actor.group.userData.cardId = id; actor.group.userData.opponent = opponent; board.add(actor.group);
    actors.push({ actor, id, opponent });
    const button = document.createElement('button'); button.type = 'button'; button.className = 'field-label'; button.textContent = `${opponent ? 'Opponent · ' : ''}${cards[id].shortName} · ${cards[id].levels[0]} BP`; button.setAttribute('aria-label', 'Inspect ' + cards[id].name); button.addEventListener('click', () => select(id)); $('object-labels').appendChild(button);
    labels.push({ element: button, position: new THREE.Vector3(x, .12, z + 1.12), id });
  }
  function refreshSolo() {
    if (soloActor) { scene.remove(soloActor.group); soloActor.dispose(); }
    soloActor = makeActor(state.selected, 2.8); soloActor.group.position.y = .04; soloActor.setSelected(true); soloActor.group.visible = state.mode === 'hologram'; scene.add(soloActor.group);
  }
  function select(id) {
    if (!cards[id]) return;
    state.selected = id; const c = cards[id];
    $('card-art').src = assets[c.cardAsset]; $('card-art').alt = c.name + ' card'; $('full-art').src = assets[c.cardAsset]; $('full-art').alt = c.name + ' enlarged card';
    $('card-name').textContent = c.name; $('card-type').textContent = c.isBoss ? 'BOSS PRESENTATION' : 'SPIRIT PRESENTATION'; $('card-code').textContent = c.code; $('card-families').textContent = c.families;
    $('card-cost').textContent = c.cost; $('card-bp').textContent = c.levels.map(n => Number(n).toLocaleString('en-US')).join(' / '); $('card-effect').textContent = c.effect.replaceAll('《', '[').replaceAll('》', ']');
    $('summon-preview').textContent = c.isBoss ? 'Preview boss summon' : 'Preview summon';
    document.querySelectorAll('[data-card]').forEach(el => { el.classList.toggle('selected', el.dataset.card === id); el.setAttribute('aria-pressed', String(el.dataset.card === id)); });
    actors.forEach(a => a.actor.setSelected(a.id === id && !a.opponent)); labels.forEach(l => l.element.classList.toggle('selected', l.id === id));
    if (state.ready) refreshSolo(); renderNow();
  }
  function phase(i) { state.phase = i; [...$('phase-list').children].forEach((li, k) => { const b = li.querySelector('button'); li.classList.toggle('active', k === i); b.setAttribute('aria-current', k === i ? 'step' : 'false'); }); $('stage-context').textContent = phases[i] + ' · Presentation scene'; }
  function setMode(mode, options = {}) {
    if (!state.ready) return;
    if (state.playing && mode !== 'cutscene') closeCutscene();
    state.mode = mode;
    for (const m of ['arena', 'hologram', 'cutscene']) { $('mode-' + m).classList.toggle('active', mode === m); $('mode-' + m).setAttribute('aria-pressed', String(mode === m)); }
    const solo = mode === 'hologram'; board.visible = !solo; soloFloor.visible = solo; if (soloActor) soloActor.group.visible = solo;
    $('hand').hidden = solo; $('hand').closest('.hand-area').hidden = solo; $('object-labels').hidden = solo; $('hologram-toggle').hidden = solo;
    document.querySelector('.player-huds').hidden = solo; document.querySelector('.opponent-hand').hidden = solo;
    document.querySelectorAll('.player-hud').forEach(el => el.hidden = solo);
    $('stage-caption').textContent = solo ? 'Hologram view' : 'Two-player arena';
    $('mode-description').textContent = solo ? 'A separate character silhouette stands above the card. Original colors stay readable through subtle scanlines and light.' : 'Mirrored fields, five Life Cores, separate Core and Soul Core, with persistent holograms and readable card details.';
    yaw = 0; pitch = .45; zoom = 1; resize();
    if (mode === 'cutscene' && options.play !== false) { select('ritenia'); showCutscene(true); } else startLoop();
  }
  function showCutscene(standalone) {
    if (!state.ready) return;
    const c = cards[state.selected];
    if (!c.isBoss) { actors.filter(a => a.id === state.selected && !a.opponent).forEach(a => a.actor.reveal()); if (soloActor) soloActor.reveal(); notify('Spirit summoned · local hologram reveal'); startLoop(); return; }
    state.playing = true; stopLoop(); $('cutscene-overlay').hidden = false; setBackgroundInert(true); $('cutscene-role').textContent = c.shortName.toUpperCase();
    $('replay-cutscene').hidden = true; $('skip-cutscene').hidden = false; $('close-cutscene').hidden = false;
    $('cutscene-overlay').dataset.standalone = String(standalone);
    cutscene.play({ image: textures[state.selected + 'Cut'].image, name: c.name, bpText: `Lv1 BP ${Number(c.levels[0]).toLocaleString('en-US')}  /  Lv2 BP ${Number(c.levels[1]).toLocaleString('en-US')}`, color: c.accent, duration: 4200, sound: state.sound, reducedMotion: state.reduced });
    if (!$('skip-cutscene').hidden) $('skip-cutscene').focus();
  }
  function onCutsceneComplete() {
    if (closingCutscene) return;
    $('skip-cutscene').hidden = true; $('replay-cutscene').hidden = false;
    if ($('cutscene-overlay').dataset.standalone === 'true') { state.playing = false; $('replay-cutscene').focus(); }
    else { closeCutscene(); actors.filter(a => a.id === state.selected && !a.opponent).forEach(a => a.actor.reveal()); if (soloActor) soloActor.reveal(); notify('Boss summoned · persistent hologram'); }
  }
  function setBackgroundInert(value) { document.querySelectorAll('.site-header,.page-heading,.arena-layout,.site-footer').forEach(el => el.inert = value); }
  function closeCutscene() { closingCutscene = true; cutscene.skip(); closingCutscene = false; $('cutscene-overlay').hidden = true; setBackgroundInert(false); state.playing = false; if (state.mode === 'cutscene') { state.mode = 'arena'; setMode('arena', { play: false }); } else startLoop(); $('summon-preview').focus(); }
  function toggleMotion() { state.reduced = !state.reduced; syncPreferences(); }
  function syncPreferences() {
    $('sound-toggle').textContent = state.sound ? 'Sound: On' : 'Sound: Off'; $('sound-toggle').setAttribute('aria-pressed', String(state.sound));
    $('motion-toggle').textContent = state.reduced ? 'Motion: Reduced' : 'Motion: Full'; $('motion-toggle').setAttribute('aria-pressed', String(state.reduced));
    actors.forEach(a => { a.actor.setReducedMotion(state.reduced); a.actor.setQuality(state.quality); }); if (soloActor) { soloActor.setReducedMotion(state.reduced); soloActor.setQuality(state.quality); }
    if (renderer) renderer.setPixelRatio(Math.min(devicePixelRatio || 1, state.quality === 'low' ? 1 : 1.75)); resize(); startLoop();
  }
  function updateCamera() {
    if (!camera) return;
    const ratio = stage.clientWidth / Math.max(1, stage.clientHeight), solo = state.mode === 'hologram';
    if (solo) { const d = 7.8 * Math.max(1, .61 / ratio) * zoom; camera.position.set(Math.sin(yaw) * Math.cos(pitch) * d, 1 + Math.sin(pitch) * d, Math.cos(yaw) * Math.cos(pitch) * d); camera.lookAt(0, 1.1, 0); }
    else { const fit = Math.max(1, 1.4 / ratio) * zoom; camera.position.set(Math.sin(yaw) * 17 * fit, (13.3 + (pitch - .45) * 6) * fit, Math.cos(yaw) * 14.8 * fit); camera.lookAt(0, .3, .5); }
  }
  function resize() { if (!renderer) return; const w = stage.clientWidth, h = stage.clientHeight; if (!w || !h) return; renderer.setSize(w, h, false); camera.aspect = w / h; camera.updateProjectionMatrix(); updateCamera(); renderNow(); }
  function renderNow(delta = 0) {
    if (!state.ready || !renderer || state.playing || !$('cutscene-overlay').hidden || document.hidden) return;
    updateCamera();
    for (const a of actors) { a.actor.group.visible = true; a.actor.setHologramVisible(state.holograms); a.actor.update(elapsed, delta, camera); }
    if (soloActor && state.mode === 'hologram') soloActor.update(elapsed, delta, camera);
    renderer.render(scene, camera);
    const p = new THREE.Vector3(), w = stage.clientWidth, h = stage.clientHeight;
    labels.forEach(l => { p.copy(l.position).project(camera); l.element.style.left = ((p.x + 1) * w / 2) + 'px'; l.element.style.top = ((1 - p.y) * h / 2) + 'px'; l.element.style.visibility = Math.abs(p.x) < .96 && Math.abs(p.y) < .95 ? 'visible' : 'hidden'; });
  }
  function stopLoop() { if (frame) cancelAnimationFrame(frame); frame = 0; }
  function startLoop() {
    if (!state.ready || document.hidden || state.playing || !$('cutscene-overlay').hidden) return;
    if (state.reduced) { stopLoop(); renderNow(); return; }
    if (frame) return;
    last = performance.now();
    const loop = now => { frame = 0; if (document.hidden || state.playing || state.reduced || !$('cutscene-overlay').hidden) return; const delta = Math.min(.06, (now - last) / 1000); last = now; elapsed += delta; if (now - lastDraw >= (state.quality === 'low' ? 33 : 16)) { renderNow(delta); lastDraw = now; } frame = requestAnimationFrame(loop); };
    frame = requestAnimationFrame(loop);
  }
  function installUI() {
    for (const c of Object.values(cards)) { const b = document.createElement('button'); b.type = 'button'; b.className = 'card-choice'; b.dataset.card = c.id; b.textContent = c.shortName; b.addEventListener('click', () => select(c.id)); $('card-picker').appendChild(b); }
    const opposingHand = document.createElement('div'); opposingHand.className = 'opponent-hand'; opposingHand.setAttribute('aria-label', 'Opponent hand: 4 hidden cards'); for (let i = 0; i < 4; i++) { const img = document.createElement('img'); img.src = assets.back; img.alt = ''; opposingHand.appendChild(img); } stage.appendChild(opposingHand);
    ['ritenia', 'pisketona', 'ritenia', 'pisketona'].forEach((id, i) => { const b = document.createElement('button'); b.type = 'button'; b.className = 'hand-card'; b.dataset.card = id; b.setAttribute('aria-label', `Inspect hand card ${i + 1}: ${cards[id].name}`); b.title = cards[id].name; const image = document.createElement('img'); image.src = assets[cards[id].cardAsset]; image.alt = ''; image.draggable = false; b.appendChild(image); b.addEventListener('click', () => select(id)); $('hand').appendChild(b); });
    phases.forEach((name, i) => { const li = document.createElement('li'), b = document.createElement('button'); b.type = 'button'; b.className = 'phase-button'; const n = document.createElement('span'); n.textContent = String(i + 1).padStart(2, '0'); b.append(n, document.createTextNode(name)); b.addEventListener('click', () => phase(i)); li.appendChild(b); $('phase-list').appendChild(li); });
    $('phase-next').addEventListener('click', () => { phase((state.phase + 1) % 8); notify(phases[state.phase]); });
    for (const m of ['arena', 'hologram', 'cutscene']) $('mode-' + m).addEventListener('click', () => setMode(m));
    $('summon-preview').addEventListener('click', () => showCutscene(state.mode === 'cutscene'));
    $('sound-toggle').addEventListener('click', () => { state.sound = !state.sound; syncPreferences(); }); $('motion-toggle').addEventListener('click', toggleMotion);
    $('quality-select').addEventListener('change', e => { state.quality = e.target.value; syncPreferences(); });
    $('camera-reset').addEventListener('click', () => { yaw = 0; pitch = .45; zoom = 1; resize(); });
    $('hologram-toggle').addEventListener('click', () => { state.holograms = !state.holograms; $('hologram-toggle').textContent = state.holograms ? 'Holograms: On' : 'Holograms: Off'; $('hologram-toggle').setAttribute('aria-pressed', String(state.holograms)); actors.forEach(a => { if (a.actor.setHologramVisible) a.actor.setHologramVisible(state.holograms); }); renderNow(); });
    $('inspect-full').addEventListener('click', () => $('card-dialog').showModal()); $('close-dialog').addEventListener('click', () => $('card-dialog').close()); $('card-dialog').addEventListener('click', e => { if (e.target === $('card-dialog')) $('card-dialog').close(); });
    $('skip-cutscene').addEventListener('click', () => cutscene.skip()); $('close-cutscene').addEventListener('click', closeCutscene); $('replay-cutscene').addEventListener('click', () => showCutscene($('cutscene-overlay').dataset.standalone === 'true'));
    addEventListener('keydown', e => { if ($('cutscene-overlay').hidden) return; if (e.key === 'Escape') closeCutscene(); if (e.key === 'Tab') { const list = [...$('cutscene-overlay').querySelectorAll('button')].filter(b => !b.hidden && b.getClientRects().length); const at = list.indexOf(document.activeElement); if (e.shiftKey && at <= 0) { e.preventDefault(); list[list.length - 1]?.focus(); } else if (!e.shiftKey && (at < 0 || at === list.length - 1)) { e.preventDefault(); list[0]?.focus(); } } });
    document.addEventListener('visibilitychange', () => { if (document.hidden) { stopLoop(); if (state.playing) cutscene.skip(); } else startLoop(); });
    $('retry-load').addEventListener('click', () => location.reload());
    phase(4); select('ritenia');
  }
  function installPointer() {
    const canvas = renderer.domElement;
    canvas.addEventListener('pointerdown', e => { if (e.button !== 0) return; dragging = true; moved = false; pointer = { x: e.clientX, y: e.clientY, startX: e.clientX, startY: e.clientY }; canvas.setPointerCapture(e.pointerId); });
    canvas.addEventListener('pointermove', e => { if (!dragging || !pointer) return; const dx = e.clientX - pointer.x, dy = e.clientY - pointer.y; moved ||= Math.hypot(e.clientX - pointer.startX, e.clientY - pointer.startY) > 5; yaw += dx * .006; if (state.mode === 'arena') yaw = Math.max(-.32, Math.min(.32, yaw)); pitch = Math.max(.18, Math.min(1.05, pitch + dy * .004)); pointer.x = e.clientX; pointer.y = e.clientY; renderNow(); });
    canvas.addEventListener('pointerup', e => { if (!dragging) return; dragging = false; if (moved || state.mode !== 'arena') return; const box = canvas.getBoundingClientRect(), mouse = new THREE.Vector2((e.clientX - box.left) / box.width * 2 - 1, 1 - (e.clientY - box.top) / box.height * 2), ray = new THREE.Raycaster(); ray.setFromCamera(mouse, camera); const hits = ray.intersectObjects(actors.map(a => a.actor.group), true); for (const hit of hits) { let node = hit.object; while (node && !node.userData.cardId) node = node.parent; if (node) { select(node.userData.cardId); break; } } });
    canvas.addEventListener('pointercancel', () => dragging = false);
    canvas.addEventListener('wheel', e => { e.preventDefault(); zoom = Math.max(.75, Math.min(1.65, zoom + e.deltaY * .0006)); renderNow(); }, { passive: false });
    canvas.addEventListener('webglcontextlost', e => { e.preventDefault(); stopLoop(); loading.hidden = false; $('load-progress').textContent = 'The 3D view was interrupted. Reload to restore it.'; $('retry-load').hidden = false; });
  }
  async function initialize() {
    installUI();
    try {
      if (!window.THREE || !assets) throw new Error('The demo files are incomplete. Extract the full ZIP and reopen index.html.');
      renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, powerPreference: 'low-power' }); renderer.outputEncoding = THREE.sRGBEncoding; renderer.setClearColor(0x050b14, 0); renderer.domElement.id = 'arena-canvas'; renderer.domElement.setAttribute('aria-label', 'Interactive Battle Spirits field'); stage.insertBefore(renderer.domElement, stage.firstChild);
      scene = new THREE.Scene(); camera = new THREE.PerspectiveCamera(42, 1, .1, 120);
      const load = (key, asset) => new Promise((resolve, reject) => { new THREE.TextureLoader().load(assets[asset], t => { t.anisotropy = Math.min(8, renderer.capabilities.getMaxAnisotropy()); t.encoding = THREE.sRGBEncoding; textures[key] = t; ownedTextures.add(t); resolve(t); }, undefined, () => reject(new Error('Cannot load ' + asset))); });
      $('load-progress').textContent = 'Preparing the field and character silhouettes…';
      await Promise.race([Promise.all([load('riteniaCard', 'riteniaCard'), load('riteniaCut', 'riteniaCut'), load('pisketonaCard', 'pisketonaCard'), load('pisketonaCut', 'pisketonaCut'), load('core', 'core'), load('soul', 'soul'), load('back', 'back')]), new Promise((_, reject) => setTimeout(() => reject(new Error('Assets took too long to load. Reload the demo.')), 15000))]);
      setupBoard(); cutscene = new window.BossCutscene($('cutscene-canvas'), { onComplete: onCutsceneComplete }); state.ready = true; select(state.selected); syncPreferences(); setMode('arena'); loading.hidden = true;
      installPointer(); new ResizeObserver(resize).observe(stage); startLoop();
      window.__BS_REVIEW__ = { state: () => ({ ...state, actorCount: actors.length, cutscene: cutscene.getState(), frameScheduled: !!frame, phases: [...phases] }), select, mode: setMode, render: renderNow, renderer, actors, cutscene };
    } catch (error) { stopLoop(); loading.hidden = false; $('load-progress').textContent = error.message; $('retry-load').hidden = false; console.error(error); }
  }
  addEventListener('pagehide', () => { stopLoop(); cutscene?.dispose(); actors.forEach(a => a.actor.dispose()); soloActor?.dispose(); ownedGeometries.forEach(g => g.dispose()); ownedMaterials.forEach(m => m.dispose()); ownedTextures.forEach(t => t.dispose()); renderer?.dispose(); });
  initialize();
})();
