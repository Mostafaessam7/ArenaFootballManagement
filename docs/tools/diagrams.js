// Generates the conceptual EERD (Chen notation) and the use case diagram as SVG in docs/diagrams.
// PNG copies for Word are rendered by render-diagrams.ps1 (Microsoft Edge, headless).
// Coordinates are explicit so the layout stays stable; edit a spec and run `npm run diagrams`.
'use strict';
const fs = require('fs');
const path = require('path');

const OUT = path.join(__dirname, '..', 'diagrams');
const FONT = "'Segoe UI', Tahoma, Arial, sans-serif";
const C = {
  ink: '#1f2937', muted: '#6b7280', card: '#b42318',
  entity: '#e8f1fb', entityLine: '#245a8d',
  ref: '#f3f4f6', refLine: '#9ca3af',
  rel: '#fff4de', relLine: '#a86512',
  attr: '#ffffff', attrLine: '#5b6472',
};

const esc = (s) => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
function textWidth(t, size) {
  let w = 0;
  for (const ch of t) {
    if (/[A-Z_]/.test(ch)) w += size * 0.66;
    else if (/[a-z0-9]/.test(ch)) w += size * 0.56;
    else if (/[؀-ۿ]/.test(ch)) w += size * 0.52;
    else w += size * 0.34;
  }
  return w;
}

// ---- geometry: where a line from the centre towards (dx, dy) leaves a shape -------------------
function clip(shape, tx, ty) {
  const dx = tx - shape.x, dy = ty - shape.y;
  if (dx === 0 && dy === 0) return [shape.x, shape.y];
  let t;
  if (shape.kind === 'rect') t = Math.min(shape.w / 2 / Math.abs(dx || 1e-9), shape.h / 2 / Math.abs(dy || 1e-9));
  else if (shape.kind === 'diamond') t = 1 / (Math.abs(dx) / (shape.w / 2) + Math.abs(dy) / (shape.h / 2));
  else if (shape.kind === 'ellipse') t = 1 / Math.sqrt((dx / shape.rx) ** 2 + (dy / shape.ry) ** 2);
  else if (shape.kind === 'circle') t = shape.r / Math.hypot(dx, dy);
  return [shape.x + dx * t, shape.y + dy * t];
}
function polyline(points, opts = {}) {
  const d = points.map((p, i) => `${i ? 'L' : 'M'}${p[0].toFixed(1)},${p[1].toFixed(1)}`).join(' ');
  return `<path d="${d}" fill="none" stroke="${opts.color || C.ink}" stroke-width="${opts.width || 1.4}"${opts.dash ? ` stroke-dasharray="${opts.dash}"` : ''}/>`;
}
// Two parallel lines = total participation.
function doubleLine(points, color) {
  const out = [];
  for (const off of [-2.6, 2.6]) {
    const shifted = points.map((p, i) => {
      const a = points[Math.max(0, i - 1)], b = points[Math.min(points.length - 1, i + 1)];
      const len = Math.hypot(b[0] - a[0], b[1] - a[1]) || 1;
      return [p[0] - (b[1] - a[1]) / len * off, p[1] + (b[0] - a[0]) / len * off];
    });
    out.push(polyline(shifted, { color, width: 1.2 }));
  }
  return out.join('');
}

// ---- Chen renderer ---------------------------------------------------------------------------
function chen(spec) {
  const shapes = {};
  const body = [];
  const front = [];
  for (const e of spec.entities) {
    const w = e.w || Math.max(118, textWidth(e.name, 14) + 34);
    shapes[e.id] = { kind: 'rect', x: e.x, y: e.y, w, h: 44, def: e };
  }
  for (const r of spec.relationships) {
    const w = r.w || Math.max(112, textWidth(r.name, 12.5) * 1.55 + 26);
    shapes[r.id] = { kind: 'diamond', x: r.x, y: r.y, w, h: 54, def: r };
  }
  for (const s of spec.specializations || []) shapes[s.id] = { kind: 'circle', x: s.x, y: s.y, r: 15, def: s };

  // attributes (drawn first so entity boxes sit on top of their connector lines)
  for (const a of spec.attributes || []) {
    const owner = shapes[a.of];
    drawAttribute(a, owner, body);
  }
  // relationship links
  for (const l of spec.links) {
    const e = shapes[l.entity], r = shapes[l.rel];
    const via = l.via || [];
    const first = via.length ? via[0] : [r.x, r.y];
    const last = via.length ? via[via.length - 1] : [e.x, e.y];
    const pts = [clip(e, ...first), ...via.slice().reverse().map((p) => p), clip(r, ...last)];
    // pts must run entity -> ... -> rel; via is given from entity side
    const ordered = [clip(e, ...(via[0] || [r.x, r.y])), ...via, clip(r, ...(via.length ? via[via.length - 1] : [e.x, e.y]))];
    body.push(l.total ? doubleLine(ordered, C.ink) : polyline(ordered));
    if (l.card) {
      // label on the segment that touches the relationship, a third of the way from the diamond
      const a = ordered[ordered.length - 1], b = ordered[ordered.length - 2];
      const len = Math.hypot(b[0] - a[0], b[1] - a[1]) || 1;
      const k = Math.min(0.42, 30 / len + 0.12);
      const mx = a[0] + (b[0] - a[0]) * k, my = a[1] + (b[1] - a[1]) * k;
      const nx = -(b[1] - a[1]) / len, ny = (b[0] - a[0]) / len;
      const side = l.labelSide || 1;
      front.push(`<text x="${(mx + nx * 11 * side).toFixed(1)}" y="${(my + ny * 11 * side + 5).toFixed(1)}" font-family="${FONT}" font-size="15" font-weight="700" fill="${C.card}" text-anchor="middle">${esc(l.card)}</text>`);
    }
    void pts;
  }
  // specialization circles
  for (const s of spec.specializations || []) {
    const sh = shapes[s.id], sup = shapes[s.super];
    const top = [clip(sup, sh.x, sh.y), clip(sh, sup.x, sup.y)];
    body.push(s.total ? doubleLine(top, C.ink) : polyline(top));
    if (s.defining) {
      const mx = (top[0][0] + top[1][0]) / 2, my = (top[0][1] + top[1][1]) / 2;
      const [ox, oy] = s.definingAt || [10, 4];
      front.push(`<text x="${mx + ox}" y="${my + oy}" font-family="${FONT}" font-size="12.5" font-style="italic" fill="${C.muted}" text-anchor="${s.definingAnchor || 'start'}">${esc(s.defining)}</text>`);
    }
    for (const subId of s.subs) {
      const sub = shapes[subId];
      const seg = [clip(sh, sub.x, sub.y), clip(sub, sh.x, sh.y)];
      body.push(polyline(seg));
      // subset symbol on the line, opening towards the superclass
      const mx = (seg[0][0] + seg[1][0]) / 2, my = (seg[0][1] + seg[1][1]) / 2;
      const ang = Math.atan2(seg[1][1] - seg[0][1], seg[1][0] - seg[0][0]) * 180 / Math.PI;
      front.push(`<path d="M -6,-7 A 7,7 0 0 0 -6,7" transform="translate(${mx.toFixed(1)},${my.toFixed(1)}) rotate(${ang.toFixed(1)}) translate(6,0)" fill="none" stroke="${C.ink}" stroke-width="1.6"/>`);
    }
    front.push(`<circle cx="${sh.x}" cy="${sh.y}" r="${sh.r}" fill="#ffffff" stroke="${C.ink}" stroke-width="1.5"/>`);
    front.push(`<text x="${sh.x}" y="${sh.y + 5}" font-family="${FONT}" font-size="15" font-weight="700" fill="${C.ink}" text-anchor="middle">${esc(s.label || 'd')}</text>`);
  }
  // relationships
  for (const r of spec.relationships) {
    const s = shapes[r.id];
    const pts = (k) => `${s.x},${s.y - s.h / 2 + k} ${s.x + s.w / 2 - k * 1.9},${s.y} ${s.x},${s.y + s.h / 2 - k} ${s.x - s.w / 2 + k * 1.9},${s.y}`;
    front.push(`<polygon points="${pts(0)}" fill="${C.rel}" stroke="${C.relLine}" stroke-width="1.6"/>`);
    if (r.identifying) front.push(`<polygon points="${pts(5)}" fill="none" stroke="${C.relLine}" stroke-width="1.3"/>`);
    front.push(`<text x="${s.x}" y="${s.y + 4.5}" font-family="${FONT}" font-size="12.5" font-weight="600" fill="${C.ink}" text-anchor="middle">${esc(r.name)}</text>`);
  }
  // entities
  for (const e of spec.entities) {
    const s = shapes[e.id];
    const fill = e.ref ? C.ref : C.entity, line = e.ref ? C.refLine : C.entityLine;
    front.push(`<rect x="${s.x - s.w / 2}" y="${s.y - s.h / 2}" width="${s.w}" height="${s.h}" rx="2" fill="${fill}" stroke="${line}" stroke-width="1.7"${e.ref ? ' stroke-dasharray="6 3"' : ''}/>`);
    if (e.weak) front.push(`<rect x="${s.x - s.w / 2 + 5}" y="${s.y - s.h / 2 + 5}" width="${s.w - 10}" height="${s.h - 10}" fill="none" stroke="${line}" stroke-width="1.3"/>`);
    front.push(`<text x="${s.x}" y="${s.y + 5}" font-family="${FONT}" font-size="14" font-weight="700" fill="${e.ref ? C.muted : C.ink}" text-anchor="middle">${esc(e.name)}</text>`);
  }
  for (const n of spec.notes || []) {
    front.push(`<text x="${n.x}" y="${n.y}" font-family="${FONT}" font-size="${n.size || 12.5}" fill="${n.color || C.muted}"${n.anchor ? ` text-anchor="${n.anchor}"` : ''}${n.italic ? ' font-style="italic"' : ''}>${esc(n.text)}</text>`);
  }
  if (spec.legend) front.push(legend(spec.legend.x, spec.legend.y));
  return svg(spec.width, spec.height, spec.title, body.join('\n') + '\n' + front.join('\n'));
}

function drawAttribute(a, owner, out, parentShape) {
  const size = 12.5;
  const rx = Math.max(38, textWidth(a.name, size) / 2 + 14), ry = 15;
  const x = (parentShape || owner).x + a.dx, y = (parentShape || owner).y + a.dy;
  const me = { kind: 'ellipse', x, y, rx, ry };
  const from = parentShape || owner;
  out.push(polyline([clip(from, x, y), clip(me, from.x, from.y)], { color: C.attrLine, width: 1.1 }));
  for (const p of a.parts || []) drawAttribute(p, owner, out, me);
  out.push(`<ellipse cx="${x}" cy="${y}" rx="${rx}" ry="${ry}" fill="${C.attr}" stroke="${C.attrLine}" stroke-width="1.2"${a.derived ? ' stroke-dasharray="5 3"' : ''}/>`);
  if (a.multi) out.push(`<ellipse cx="${x}" cy="${y}" rx="${rx - 4}" ry="${ry - 4}" fill="none" stroke="${C.attrLine}" stroke-width="1"/>`);
  out.push(`<text x="${x}" y="${y + 4.3}" font-family="${FONT}" font-size="${size}" fill="${C.ink}" text-anchor="middle">${esc(a.name)}</text>`);
  if (a.key || a.partial) {
    const half = textWidth(a.name, size) / 2;
    out.push(`<line x1="${x - half}" y1="${y + 7}" x2="${x + half}" y2="${y + 7}" stroke="${C.ink}" stroke-width="1.2"${a.partial ? ' stroke-dasharray="4 2.5"' : ''}/>`);
  }
}

function legend(x, y) {
  const g = [];
  const row = (i, shape, label) => {
    const yy = y + 26 + i * 30;
    g.push(shape(x + 34, yy));
    g.push(`<text x="${x + 70}" y="${yy + 4.5}" font-family="${FONT}" font-size="12" fill="${C.ink}">${esc(label)}</text>`);
  };
  g.push(`<rect x="${x}" y="${y}" width="300" height="${26 + 9 * 30}" rx="6" fill="#ffffff" stroke="#d1d5db"/>`);
  g.push(`<text x="${x + 12}" y="${y + 17}" font-family="${FONT}" font-size="12.5" font-weight="700" fill="${C.ink}">Notation (Chen / EER)</text>`);
  row(0.4, (cx, cy) => `<rect x="${cx - 22}" y="${cy - 9}" width="44" height="18" fill="${C.entity}" stroke="${C.entityLine}"/>`, 'Entity');
  row(1.4, (cx, cy) => `<rect x="${cx - 22}" y="${cy - 9}" width="44" height="18" fill="${C.entity}" stroke="${C.entityLine}"/><rect x="${cx - 18}" y="${cy - 5}" width="36" height="10" fill="none" stroke="${C.entityLine}"/>`, 'Weak entity');
  row(2.4, (cx, cy) => `<polygon points="${cx},${cy - 11} ${cx + 22},${cy} ${cx},${cy + 11} ${cx - 22},${cy}" fill="${C.rel}" stroke="${C.relLine}"/>`, 'Relationship');
  row(3.4, (cx, cy) => `<polygon points="${cx},${cy - 11} ${cx + 22},${cy} ${cx},${cy + 11} ${cx - 22},${cy}" fill="${C.rel}" stroke="${C.relLine}"/><polygon points="${cx},${cy - 6} ${cx + 13},${cy} ${cx},${cy + 6} ${cx - 13},${cy}" fill="none" stroke="${C.relLine}"/>`, 'Identifying relationship');
  row(4.4, (cx, cy) => `<ellipse cx="${cx}" cy="${cy}" rx="22" ry="9" fill="#fff" stroke="${C.attrLine}"/><line x1="${cx - 12}" y1="${cy + 4}" x2="${cx + 12}" y2="${cy + 4}" stroke="${C.ink}"/>`, 'Key attribute (dashed = partial key)');
  row(5.4, (cx, cy) => `<ellipse cx="${cx}" cy="${cy}" rx="22" ry="9" fill="#fff" stroke="${C.attrLine}" stroke-dasharray="4 2"/>`, 'Derived attribute (not stored)');
  row(6.4, (cx, cy) => `<line x1="${cx - 24}" y1="${cy - 2.5}" x2="${cx + 24}" y2="${cy - 2.5}" stroke="${C.ink}"/><line x1="${cx - 24}" y1="${cy + 2.5}" x2="${cx + 24}" y2="${cy + 2.5}" stroke="${C.ink}"/>`, 'Total participation (single = partial)');
  row(7.4, (cx, cy) => `<text x="${cx - 14}" y="${cy + 5}" font-family="${FONT}" font-size="14" font-weight="700" fill="${C.card}">1 N M</text>`, 'Cardinality ratio');
  row(8.4, (cx, cy) => `<circle cx="${cx}" cy="${cy}" r="10" fill="#fff" stroke="${C.ink}"/><text x="${cx}" y="${cy + 4.5}" font-family="${FONT}" font-size="12" font-weight="700" text-anchor="middle">d</text>`, 'Disjoint specialization');
  return g.join('');
}

function svg(w, h, title, inner) {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">
<rect width="${w}" height="${h}" fill="#ffffff"/>
<text x="${w / 2}" y="34" font-family="${FONT}" font-size="21" font-weight="700" fill="${C.ink}" text-anchor="middle">${esc(title)}</text>
${inner}
</svg>
`;
}

// ---- shared pieces ---------------------------------------------------------------------------
const A = (of, name, dx, dy, extra = {}) => ({ of, name, dx, dy, ...extra });

// ---- 06 overview: every entity and relationship, no attributes ------------------------------
const overview = {
  title: 'Arena EERD - entities and relationships',
  width: 2180, height: 1240,
  entities: [
    { id: 'player', name: 'PLAYER', x: 180, y: 120 },
    { id: 'membership', name: 'MEMBERSHIP', x: 180, y: 420, weak: true },
    { id: 'team', name: 'TEAM', x: 560, y: 420 },
    { id: 'roster', name: 'ROSTER_ENTRY', x: 900, y: 120, weak: true },
    { id: 'registration', name: 'REGISTRATION', x: 900, y: 420, weak: true },
    { id: 'tournament', name: 'TOURNAMENT', x: 1260, y: 420 },
    { id: 'user', name: 'USER', x: 900, y: 760 },
    { id: 'match', name: 'MATCH', x: 1560, y: 420 },
    { id: 'event', name: 'SPORT_EVENT', x: 1560, y: 760 },
    { id: 'community', name: 'COMMUNITY_EVENT', x: 1560, y: 1060 },
    { id: 'venue', name: 'VENUE', x: 1260, y: 1060 },
    { id: 'audit', name: 'RESULT_AUDIT', x: 1360, y: 620 },
    { id: 'offer', name: 'TICKET_OFFER', x: 900, y: 1060 },
    { id: 'booking', name: 'BOOKING', x: 560, y: 1060 },
    { id: 'ticket', name: 'TICKET', x: 180, y: 1060 },
  ],
  relationships: [
    { id: 'hasMembership', name: 'HAS', x: 180, y: 270, identifying: true },
    { id: 'inTeam', name: 'IN_TEAM', x: 370, y: 420 },
    { id: 'selected', name: 'SELECTED', x: 540, y: 120, identifying: true },
    { id: 'enters', name: 'ENTERS', x: 730, y: 420, identifying: true },
    { id: 'lists', name: 'LISTS', x: 900, y: 270, identifying: true },
    { id: 'includes', name: 'INCLUDES', x: 1080, y: 420, identifying: true },
    { id: 'home', name: 'HOME', x: 1230, y: 225 },
    { id: 'away', name: 'AWAY', x: 1230, y: 315 },
    { id: 'plays', name: 'PLAYS', x: 1560, y: 120 },
    { id: 'records', name: 'RECORDS', x: 1470, y: 525 },
    { id: 'manages', name: 'MANAGES', x: 560, y: 600 },
    { id: 'orgT', name: 'ORGANIZES', x: 1080, y: 590 },
    { id: 'orgE', name: 'ORGANIZES', x: 1230, y: 760 },
    { id: 'approves', name: 'APPROVES', x: 1140, y: 690 },
    { id: 'heldAt', name: 'HELD_AT', x: 1400, y: 920 },
    { id: 'sells', name: 'SELLS', x: 1230, y: 920 },
    { id: 'places', name: 'PLACES', x: 730, y: 910 },
    { id: 'reserves', name: 'RESERVES', x: 730, y: 1060 },
    { id: 'issues', name: 'ISSUES', x: 370, y: 1060 },
    { id: 'checksIn', name: 'CHECKS_IN', x: 370, y: 760 },
  ],
  specializations: [{ id: 'spec', super: 'event', subs: ['match', 'community'], x: 1760, y: 760, total: true, defining: 'EventType', definingAt: [0, -12], definingAnchor: 'middle' }],
  links: [
    { entity: 'player', rel: 'hasMembership', card: '1' },
    { entity: 'membership', rel: 'hasMembership', card: 'N', total: true },
    { entity: 'membership', rel: 'inTeam', card: 'N', total: true },
    { entity: 'team', rel: 'inTeam', card: '1' },
    { entity: 'player', rel: 'selected', card: '1' },
    { entity: 'roster', rel: 'selected', card: 'N', total: true },
    { entity: 'team', rel: 'enters', card: '1' },
    { entity: 'registration', rel: 'enters', card: 'N', total: true },
    { entity: 'registration', rel: 'lists', card: '1' },
    { entity: 'roster', rel: 'lists', card: 'N', total: true },
    { entity: 'tournament', rel: 'includes', card: '1' },
    { entity: 'registration', rel: 'includes', card: 'N', total: true },
    { entity: 'registration', rel: 'home', card: '1' },
    { entity: 'match', rel: 'home', card: 'N', total: true },
    { entity: 'registration', rel: 'away', card: '1' },
    { entity: 'match', rel: 'away', card: 'N', total: true },
    { entity: 'roster', rel: 'plays', card: 'M', via: [[900, 40], [1560, 40]] },
    { entity: 'match', rel: 'plays', card: 'N', via: [[1700, 420], [1700, 120]] },
    { entity: 'match', rel: 'records', card: '1' },
    { entity: 'audit', rel: 'records', card: 'N', total: true },
    { entity: 'user', rel: 'approves', card: '1' },
    { entity: 'audit', rel: 'approves', card: 'N', total: true },
    { entity: 'user', rel: 'manages', card: '1' },
    { entity: 'team', rel: 'manages', card: 'N' },
    { entity: 'user', rel: 'orgT', card: '1' },
    { entity: 'tournament', rel: 'orgT', card: 'N', total: true },
    { entity: 'user', rel: 'orgE', card: '1' },
    { entity: 'event', rel: 'orgE', card: 'N', total: true },
    { entity: 'venue', rel: 'heldAt', card: '1' },
    { entity: 'event', rel: 'heldAt', card: 'N', total: true },
    { entity: 'event', rel: 'sells', card: '1' },
    { entity: 'offer', rel: 'sells', card: 'N', total: true },
    { entity: 'user', rel: 'places', card: '1' },
    { entity: 'booking', rel: 'places', card: 'N', total: true },
    { entity: 'offer', rel: 'reserves', card: '1' },
    { entity: 'booking', rel: 'reserves', card: 'N', total: true },
    { entity: 'booking', rel: 'issues', card: '1', total: true },
    { entity: 'ticket', rel: 'issues', card: 'N', total: true },
    { entity: 'user', rel: 'checksIn', card: '1' },
    { entity: 'ticket', rel: 'checksIn', card: 'N', via: [[180, 760]] },
  ],
  notes: [
    { x: 40, y: 1180, text: 'Attributes are shown in the three area diagrams. Standings, totals and remaining stock are derived, not stored.' },
    { x: 40, y: 1204, text: 'REGISTRATION = a team in a tournament. ROSTER_ENTRY = a player fixed to one registration. MATCH sides reference registrations.' },
  ],
  legend: { x: 1850, y: 70 },
};

// ---- 07 teams, players and competitions ------------------------------------------------------
const competitions = {
  title: 'EERD (1/3) - Teams, players and competitions',
  width: 1560, height: 980,
  entities: [
    { id: 'player', name: 'PLAYER', x: 260, y: 300 },
    { id: 'membership', name: 'MEMBERSHIP', x: 260, y: 700, weak: true },
    { id: 'team', name: 'TEAM', x: 760, y: 700 },
    { id: 'roster', name: 'ROSTER_ENTRY', x: 600, y: 140, weak: true },
    { id: 'registration', name: 'REGISTRATION', x: 940, y: 300, weak: true },
    { id: 'tournament', name: 'TOURNAMENT', x: 1330, y: 300 },
    { id: 'user', name: 'USER', x: 1330, y: 840, ref: true },
  ],
  relationships: [
    { id: 'has', name: 'HAS', x: 260, y: 500, identifying: true },
    { id: 'inTeam', name: 'IN_TEAM', x: 510, y: 700 },
    { id: 'selected', name: 'SELECTED', x: 420, y: 220, identifying: true },
    { id: 'lists', name: 'LISTS', x: 780, y: 220, identifying: true },
    { id: 'enters', name: 'ENTERS', x: 850, y: 500, identifying: true },
    { id: 'includes', name: 'INCLUDES', x: 1140, y: 300, identifying: true },
    { id: 'manages', name: 'MANAGES', x: 1040, y: 770 },
    { id: 'organizes', name: 'ORGANIZES', x: 1330, y: 570 },
  ],
  attributes: [
    A('player', 'PlayerId', -150, -70, { key: true }), A('player', 'FullName', -170, -10), A('player', 'BirthDate', -160, 50),
    A('player', 'Nationality', -110, 110), A('player', 'Position', 140, 85),
    A('membership', 'JoinedOn', -110, 110, { partial: true }), A('membership', 'LeftOn', 60, 110),
    A('team', 'TeamId', -60, 110, { key: true }), A('team', 'Name', 70, 110), A('team', 'Code', -140, -80),
    A('team', 'City', -150, 90), A('team', 'IsActive', 180, -20),
    A('roster', 'ShirtNumber', 0, -75),
    A('tournament', 'TournamentId', -40, -120, { key: true }), A('tournament', 'Name', 120, -110), A('tournament', 'Season', 175, -45),
    A('tournament', 'Status', 175, 40),
    A('tournament', 'Period', -150, 120, { parts: [{ name: 'StartDate', dx: -75, dy: 60 }, { name: 'EndDate', dx: 70, dy: 60 }] }),
  ],
  links: [
    { entity: 'player', rel: 'has', card: '1' }, { entity: 'membership', rel: 'has', card: 'N', total: true },
    { entity: 'membership', rel: 'inTeam', card: 'N', total: true }, { entity: 'team', rel: 'inTeam', card: '1' },
    { entity: 'player', rel: 'selected', card: '1' }, { entity: 'roster', rel: 'selected', card: 'N', total: true },
    { entity: 'registration', rel: 'lists', card: '1' }, { entity: 'roster', rel: 'lists', card: 'N', total: true },
    { entity: 'team', rel: 'enters', card: '1' }, { entity: 'registration', rel: 'enters', card: 'N', total: true },
    { entity: 'tournament', rel: 'includes', card: '1' }, { entity: 'registration', rel: 'includes', card: 'N', total: true },
    { entity: 'user', rel: 'manages', card: '1' }, { entity: 'team', rel: 'manages', card: 'N' },
    { entity: 'user', rel: 'organizes', card: '1' }, { entity: 'tournament', rel: 'organizes', card: 'N', total: true },
  ],
  notes: [
    { x: 40, y: 900, text: 'Rules not shown by cardinality: a player has at most one open MEMBERSHIP (LeftOn empty); a player has one ROSTER_ENTRY per tournament;' },
    { x: 40, y: 922, text: 'shirt numbers are unique inside a registration; Code is a unique candidate key of TEAM; (Name, Season) is unique for TOURNAMENT.' },
    { x: 40, y: 944, text: 'USER (grey, dashed) is detailed in diagram 3/3.' },
  ],
};

// ---- 08 events, matches and results ----------------------------------------------------------
const events = {
  title: 'EERD (2/3) - Events, matches and results',
  width: 1560, height: 1290,
  entities: [
    { id: 'event', name: 'SPORT_EVENT', x: 700, y: 260 },
    { id: 'match', name: 'MATCH', x: 450, y: 700 },
    { id: 'community', name: 'COMMUNITY_EVENT', x: 960, y: 700 },
    { id: 'venue', name: 'VENUE', x: 1290, y: 260 },
    { id: 'audit', name: 'RESULT_AUDIT', x: 450, y: 1060 },
    { id: 'registration', name: 'REGISTRATION', x: 130, y: 430, weak: true, ref: true },
    { id: 'roster', name: 'ROSTER_ENTRY', x: 130, y: 760, weak: true, ref: true },
    { id: 'user', name: 'USER', x: 1300, y: 1060, ref: true },
  ],
  relationships: [
    { id: 'heldAt', name: 'HELD_AT', x: 1010, y: 260 },
    { id: 'home', name: 'HOME', x: 300, y: 490 },
    { id: 'away', name: 'AWAY', x: 300, y: 590 },
    { id: 'plays', name: 'PLAYS', x: 300, y: 870 },
    { id: 'records', name: 'RECORDS', x: 450, y: 900 },
    { id: 'approves', name: 'APPROVES', x: 900, y: 1060 },
    { id: 'organizes', name: 'ORGANIZES', x: 1300, y: 640 },
  ],
  specializations: [{ id: 'spec', super: 'event', subs: ['match', 'community'], x: 700, y: 470, total: true, defining: 'EventType' }],
  attributes: [
    A('event', 'EventId', -200, -70, { key: true }), A('event', 'Title', -60, -110), A('event', 'Status', 80, -110),
    A('event', 'EventType', -200, 10),
    A('event', 'Schedule', 220, -100, { parts: [{ name: 'StartsAtUtc', dx: -40, dy: -55 }, { name: 'EndsAtUtc', dx: 110, dy: -30 }] }),
    A('match', 'RoundNumber', 200, -45), A('match', 'Leg', 200, 15), A('match', 'HomeScore', 185, 75), A('match', 'AwayScore', 95, 120),
    A('community', 'Description', 0, 90),
    A('venue', 'VenueId', -40, -100, { key: true }), A('venue', 'Name', 90, -95), A('venue', 'City', 165, -40),
    A('venue', 'Address', 170, 25), A('venue', 'Capacity', 120, 85),
    A('audit', 'AuditId', 175, -60, { key: true }), A('audit', 'Reason', 300, -55), A('audit', 'CreatedUtc', -195, 30),
    A('audit', 'HomeScore', -150, 90), A('audit', 'AwayScore', -20, 105),
    A('audit', 'PreviousHomeScore', 140, 105), A('audit', 'PreviousAwayScore', 330, 70),
    A('plays', 'Goals', -170, 30), A('plays', 'Assists', -180, 90), A('plays', 'YellowCards', 70, 105),
    A('plays', 'RedCards', -25, 120), A('plays', 'MinutesPlayed', -165, 150),
  ],
  links: [
    { entity: 'venue', rel: 'heldAt', card: '1' }, { entity: 'event', rel: 'heldAt', card: 'N', total: true },
    { entity: 'registration', rel: 'home', card: '1' }, { entity: 'match', rel: 'home', card: 'N', total: true },
    { entity: 'registration', rel: 'away', card: '1', via: [[130, 590]] }, { entity: 'match', rel: 'away', card: 'N', total: true },
    { entity: 'roster', rel: 'plays', card: 'M' }, { entity: 'match', rel: 'plays', card: 'N' },
    { entity: 'match', rel: 'records', card: '1' }, { entity: 'audit', rel: 'records', card: 'N', total: true },
    { entity: 'user', rel: 'approves', card: '1' }, { entity: 'audit', rel: 'approves', card: 'N', total: true },
    { entity: 'user', rel: 'organizes', card: '1' }, { entity: 'event', rel: 'organizes', card: 'N', total: true },
  ],
  notes: [
    { x: 40, y: 1230, text: 'SPORT_EVENT is a total, disjoint specialization (EventType = Match | Community). HOME and AWAY registrations must differ and belong to the match\'s tournament.' },
    { x: 40, y: 1254, text: 'PLAYS holds one statistics row per player and match; attributed goals cannot exceed the team score. Standings are derived from finished matches.' },
  ],
};

// ---- 09 ticketing and accounts ----------------------------------------------------------------
const ticketing = {
  title: 'EERD (3/3) - Accounts and ticketing',
  width: 1640, height: 980,
  entities: [
    { id: 'user', name: 'USER', x: 260, y: 300 },
    { id: 'booking', name: 'BOOKING', x: 760, y: 300 },
    { id: 'ticket', name: 'TICKET', x: 760, y: 680 },
    { id: 'offer', name: 'TICKET_OFFER', x: 1240, y: 300 },
    { id: 'event', name: 'SPORT_EVENT', x: 1240, y: 680, ref: true },
  ],
  relationships: [
    { id: 'places', name: 'PLACES', x: 510, y: 300 },
    { id: 'reserves', name: 'RESERVES', x: 1000, y: 300 },
    { id: 'issues', name: 'ISSUES', x: 760, y: 490 },
    { id: 'checksIn', name: 'CHECKS_IN', x: 260, y: 680 },
    { id: 'sells', name: 'SELLS', x: 1240, y: 490 },
  ],
  attributes: [
    A('user', 'UserId', -170, -80, { key: true }), A('user', 'FullName', -40, -115), A('user', 'Email', 100, -110),
    A('user', 'PasswordHash', -185, -15), A('user', 'Role', -185, 50), A('user', 'IsActive', -120, 115),
    A('booking', 'BookingId', -150, -100, { key: true }), A('booking', 'Quantity', -20, -120), A('booking', 'UnitPrice', 110, -110),
    A('booking', 'Status', 170, -50), A('booking', 'IdempotencyKey', -200, -45), A('booking', 'CreatedUtc', -175, 75),
    A('booking', 'CancelledUtc', 190, 70), A('booking', 'TotalAmount', 115, 135, { derived: true }),
    A('ticket', 'TicketId', -170, -40, { key: true }), A('ticket', 'Code', -150, 60), A('ticket', 'CheckedInUtc', 40, 105),
    A('offer', 'OfferId', -160, -95, { key: true }), A('offer', 'Name', -45, -120), A('offer', 'Price', 70, -120),
    A('offer', 'Capacity', 200, -30), A('offer', 'SoldQuantity', 210, 30), A('offer', 'Remaining', 180, 95, { derived: true }),
    A('offer', 'SalesWindow', 215, -105, { parts: [{ name: 'SalesStartUtc', dx: -60, dy: -55 }, { name: 'SalesEndUtc', dx: 70, dy: -55 }] }),
  ],
  links: [
    { entity: 'user', rel: 'places', card: '1' }, { entity: 'booking', rel: 'places', card: 'N', total: true },
    { entity: 'offer', rel: 'reserves', card: '1' }, { entity: 'booking', rel: 'reserves', card: 'N', total: true },
    { entity: 'booking', rel: 'issues', card: '1', total: true }, { entity: 'ticket', rel: 'issues', card: 'N', total: true },
    { entity: 'user', rel: 'checksIn', card: '1' }, { entity: 'ticket', rel: 'checksIn', card: 'N' },
    { entity: 'event', rel: 'sells', card: '1' }, { entity: 'offer', rel: 'sells', card: 'N', total: true },
  ],
  notes: [
    { x: 40, y: 910, text: 'USER.Role is one of Admin, Organizer, TeamManager, Spectator, GateStaff (one role per account). Email is a unique candidate key.' },
    { x: 40, y: 932, text: 'A booking issues exactly Quantity tickets; (User, IdempotencyKey) is unique. Sum of offer capacities per event <= venue capacity.' },
    { x: 40, y: 954, text: 'TotalAmount = Quantity x UnitPrice and Remaining = Capacity - SoldQuantity are derived. Ticket Code is unique.' },
  ],
  legend: { x: 840, y: 560 },
};

// ---- 10 use case diagram ------------------------------------------------------------------------
function useCases() {
  const W = 1500, H = 1060;
  const g = [];
  const actor = (id, label, sub, x, y) => {
    g.push(`<g stroke="${C.ink}" stroke-width="2" fill="none"><circle cx="${x}" cy="${y - 34}" r="13"/><line x1="${x}" y1="${y - 21}" x2="${x}" y2="${y + 12}"/><line x1="${x - 22}" y1="${y - 8}" x2="${x + 22}" y2="${y - 8}"/><line x1="${x}" y1="${y + 12}" x2="${x - 17}" y2="${y + 38}"/><line x1="${x}" y1="${y + 12}" x2="${x + 17}" y2="${y + 38}"/></g>`);
    g.push(`<text x="${x}" y="${y + 60}" font-family="${FONT}" font-size="15" font-weight="700" fill="${C.ink}" text-anchor="middle" direction="rtl">${esc(label)}</text>`);
    g.push(`<text x="${x}" y="${y + 78}" font-family="${FONT}" font-size="12" fill="${C.muted}" text-anchor="middle">${esc(sub)}</text>`);
    return { id, x, y };
  };
  const ucs = {};
  const uc = (id, label, x, y, planned) => {
    const rx = 168, ry = 27;
    ucs[id] = { x, y, rx, ry };
    return () => {
      g.push(`<ellipse cx="${x}" cy="${y}" rx="${rx}" ry="${ry}" fill="${planned ? '#ffffff' : C.entity}" stroke="${planned ? C.muted : C.entityLine}" stroke-width="1.5"${planned ? ' stroke-dasharray="6 4"' : ''}/>`);
      g.push(`<text x="${x}" y="${y + 5}" font-family="${FONT}" font-size="14" fill="${C.ink}" text-anchor="middle" direction="rtl">${esc(id + '  ' + label)}</text>`);
    };
  };
  const drawers = [
    uc('UC01', 'التسجيل وتسجيل الدخول', 520, 140),
    uc('UC11', 'عرض الجدول والترتيب والهدافين', 520, 220),
    uc('UC17', 'استعادة كلمة المرور (لاحق)', 520, 300, true),
    uc('UC08', 'حجز تذاكر', 520, 430),
    uc('UC09', 'إلغاء حجز', 520, 515),
    uc('UC15', 'إدارة الفريق ذاتيًا (لاحق)', 520, 890, true),
    uc('UC02', 'إدارة الفرق واللاعبين والانتقالات', 960, 135),
    uc('UC03', 'تجهيز بطولة وقوائم لاعبيها', 960, 205),
    uc('UC04', 'جدولة مباراة أو فعالية', 960, 275),
    uc('UC05', 'اعتماد النتيجة وتصحيحها', 960, 345),
    uc('UC06', 'تسجيل إحصائيات اللاعبين', 960, 415),
    uc('UC07', 'إنشاء فئات التذاكر', 960, 485),
    uc('UC13', 'تعديل أو إلغاء فعالية (لاحق)', 960, 555, true),
    uc('UC14', 'إنهاء البطولة (لاحق)', 960, 625, true),
    uc('UC16', 'البحث والتقارير (لاحق)', 960, 695, true),
    uc('UC12', 'إدارة حسابات الموظفين (لاحق)', 960, 800, true),
    uc('UC10', 'فحص التذكرة عند الدخول', 960, 940),
  ];
  // system boundary
  g.push(`<rect x="310" y="62" width="880" height="950" rx="10" fill="#fbfdff" stroke="${C.entityLine}" stroke-width="1.6"/>`);
  g.push(`<text x="750" y="88" font-family="${FONT}" font-size="15" font-weight="700" fill="${C.entityLine}" text-anchor="middle">Arena Football Management API</text>`);
  const visitor = actor('visitor', 'زائر', 'Visitor', 140, 170);
  const spectator = actor('spectator', 'الجمهور', 'Spectator', 140, 470);
  const manager = actor('manager', 'مدير الفريق', 'TeamManager', 140, 860);
  const organizer = actor('organizer', 'المنظم', 'Organizer', 1370, 330);
  const admin = actor('admin', 'مدير النظام', 'Admin', 1370, 680);
  const gate = actor('gate', 'موظف البوابة', 'GateStaff', 1370, 920);
  const assoc = (a, id) => {
    const u = ucs[id];
    const side = a.x < u.x ? -1 : 1;
    const ex = u.x + side * u.rx * 0.98, ey = u.y;
    const ax = a.x + (side < 0 ? 28 : -28), ay = a.y;
    g.push(polyline([[ax, ay], [ex, ey]], { color: '#4b5563', width: 1.3 }));
  };
  // generalization: child -> parent with a hollow triangle at the parent
  const general = (child, parent) => {
    const x1 = child.x, y1 = child.y + (parent.y < child.y ? -56 : 92), x2 = parent.x, y2 = parent.y + (parent.y < child.y ? 92 : -56);
    const ang = Math.atan2(y2 - y1, x2 - x1);
    const bx = x2 - Math.cos(ang) * 16, by = y2 - Math.sin(ang) * 16;
    g.push(polyline([[x1, y1], [bx, by]], { color: C.ink, width: 1.4 }));
    const p = (dA, r) => `${(x2 - Math.cos(ang + dA) * r).toFixed(1)},${(y2 - Math.sin(ang + dA) * r).toFixed(1)}`;
    g.push(`<polygon points="${x2},${y2} ${p(0.45, 18)} ${p(-0.45, 18)}" fill="#ffffff" stroke="${C.ink}" stroke-width="1.4"/>`);
  };
  general(spectator, visitor);
  general(admin, organizer);
  general(admin, gate);
  for (const id of ['UC01', 'UC11', 'UC17']) assoc(visitor, id);
  for (const id of ['UC08', 'UC09']) assoc(spectator, id);
  assoc(manager, 'UC15');
  for (const id of ['UC02', 'UC03', 'UC04', 'UC05', 'UC06', 'UC07', 'UC13', 'UC14', 'UC16']) assoc(organizer, id);
  assoc(admin, 'UC12');
  assoc(gate, 'UC10');
  for (const d of drawers) d();
  g.push(`<text x="40" y="${H - 18}" font-family="${FONT}" font-size="12.5" fill="${C.muted}">Solid ellipse = implemented in week 1; dashed = analysed, planned. Hollow arrow = actor inherits the use cases of the parent actor (Spectator is a signed-in Visitor; Admin can do everything Organizer and GateStaff do).</text>`);
  return svg(W, H, 'Use case diagram', g.join('\n'));
}

const outputs = {
  '06_EERD_Overview.svg': chen(overview),
  '07_EERD_Teams_and_Competitions.svg': chen(competitions),
  '08_EERD_Events_and_Results.svg': chen(events),
  '09_EERD_Accounts_and_Ticketing.svg': chen(ticketing),
  '10_Use_Case_Diagram.svg': useCases(),
};
for (const [name, content] of Object.entries(outputs)) {
  fs.writeFileSync(path.join(OUT, name), content);
  console.log('wrote', name);
}
