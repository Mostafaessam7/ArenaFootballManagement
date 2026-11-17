// Builds the Word deliverables from the Markdown sources in docs/.
//   docs/01_System_Analysis.md              -> docs/01_System_Analysis.docx
//   docs/02_Database_Design_and_Mapping.md  -> docs/02_Database_Design_and_Mapping.docx
// The Markdown is the source of truth: edit it, run `npm run docs`, then `word-export.ps1` to refresh
// the table of contents (and optionally make PDFs).
//
// Markdown subset: front matter (title/subtitle/header), # cover title, ## / ### / #### headings,
// paragraphs, - bullets, 1. numbered lists, > notes, ``` code, pipe tables, ![caption](path) figures,
// **bold**, `code`, <br> inside table cells. Directives: [[TOC]], <!-- pagebreak -->,
// <!-- landscape --> / <!-- portrait -->, <!-- table: kv -->, <!-- widths: 20,50,30 -->.
// Sections between <!-- BEGIN generated:name --> and <!-- END generated:name --> are rewritten
// from database/schema.json on every build.
'use strict';
const fs = require('fs');
const path = require('path');
const d = require('docx');

const DOCS = path.join(__dirname, '..');
const ROOT = path.join(DOCS, '..');
const FONT = 'Arial';
const MONO = 'Consolas';
const COLOR = { heading: '1F3864', accent: '2B5C8A', muted: '6B7280', headerFill: '1F3864', kvFill: 'E8EEF7', zebra: 'F7F9FC', border: 'BFC7D5', note: 'EEF4FB', code: 'F3F4F6' };
const PAGE = { w: 11906, h: 16838, margin: 1134 };   // A4, 2 cm margins

// ---------------------------------------------------------------- generated Markdown sections
const TABLE_NOTES = {
  Users: 'حساب واحد بدور واحد؛ البريد بعد التوحيد مفتاح مرشح. PasswordHash لا يعود في أي استجابة.',
  Teams: 'كود ثابت وفريد ومدير اختياري. IsActive للأرشفة دون حذف التاريخ.',
  Players: 'هوية اللاعب مستقلة عن فريقه الحالي؛ الفريق يُعرف من العضويات.',
  PlayerMemberships: 'الكيان الضعيف MEMBERSHIP: فترات عضوية اللاعب، فترة واحدة فقط مفتوحة. النهاية غير شاملة.',
  Tournaments: 'بطولة دوري لموسم وفترة، ينظمها مستخدم.',
  TournamentTeams: 'الكيان الضعيف REGISTRATION: فريق مسجل في بطولة؛ المفتاح البديل (TournamentId, TeamId) هو مرجع المباريات والقوائم.',
  TournamentRosters: 'الكيان الضعيف ROSTER_ENTRY: لاعب مثبت في تسجيل فريق داخل بطولة.',
  Venues: 'ملاعب وأماكن الفعاليات وسعتها للدخول العام.',
  Events: 'التخصص SPORT_EVENT بنوعيه في جدول واحد (TPH)؛ الأعمدة الخاصة بكل نوع يفرضها CHECK حسب EventType.',
  MatchPlayerStats: 'العلاقة M:N المسماة PLAYS بين المباراة وقائمة البطولة، مع إحصائيات اللاعب.',
  ResultAudits: 'سجل كل اعتماد أو تصحيح لنتيجة مع القيم السابقة والفاعل.',
  TicketOffers: 'فئات التذاكر: السعر والسعة ونافذة البيع؛ SoldQuantity يتغير بأمر UPDATE شرطي واحد.',
  Bookings: 'حجز لعرض واحد بسعر محفوظ وقت الحجز ومفتاح Idempotency. لا يمثل تحصيلًا ماليًا.',
  Tickets: 'تذكرة فردية بكود عشوائي فريد، تُفحص مرة واحدة.',
};
const TABLE_ORDER = ['Users', 'Teams', 'Players', 'PlayerMemberships', 'Tournaments', 'TournamentTeams', 'TournamentRosters',
  'Venues', 'Events', 'MatchPlayerStats', 'ResultAudits', 'TicketOffers', 'Bookings', 'Tickets'];

function loadSchema() {
  const raw = fs.readFileSync(path.join(ROOT, 'database', 'schema.json'), 'utf8').replace(/^﻿/, '');
  const tables = JSON.parse(raw);
  return TABLE_ORDER.map((n) => tables.find((t) => t.Name === n)).filter(Boolean)
    .concat(tables.filter((t) => !TABLE_ORDER.includes(t.Name)));
}
const fkOf = (t, col) => t.ForeignKeys.find((f) => f.Columns.includes(col));
function generated(name, schema) {
  const out = [];
  if (name === 'data-dictionary') {
    for (const t of schema) {
      out.push(`### ${t.Name}`, '', TABLE_NOTES[t.Name] || '', '', '<!-- widths: 26,22,10,42 -->', '| العمود | النوع في SQL Server | NULL | المفتاح والملاحظات |', '|---|---|---|---|');
      for (const c of t.Columns) {
        const notes = [];
        if (c.PrimaryKey) notes.push('PK');
        const fk = fkOf(t, c.Name);
        if (fk) notes.push(`FK → ${fk.ReferencedTable}(${fk.ReferencedColumns[fk.Columns.indexOf(c.Name)]})`);
        if (c.Name === 'Version') notes.push('Concurrency token');
        if (t.Name === 'Events' && c.Name === 'EventType') notes.push('Discriminator: Match | Community');
        out.push(`| ${c.Name} | ${c.StoreType} | ${c.IsNullable ? 'نعم' : 'لا'} | ${notes.join('، ') || '—'} |`);
      }
      const pk = t.Columns.filter((c) => c.PrimaryKey).map((c) => c.Name).join(',');
      const uniques = [...t.UniqueConstraints.filter((u) => u.Columns.join(',') !== pk).map((u) => `(${u.Columns.join(', ')}) مفتاح بديل`),
        ...t.Indexes.filter((i) => i.IsUnique).map((i) => `(${i.Columns.join(', ')})${i.Filter ? `|حيث|${i.Filter}` : ''}`)];
      // code spans hold only SQL; Arabic words stay outside them
      const fmt = (u) => {
        if (u.endsWith(' مفتاح بديل')) return '`' + u.replace(/ مفتاح بديل$/, '') + '` (مفتاح بديل)';
        const [cols, , filter] = u.split('|');
        return '`' + cols + '`' + (filter ? ` حيث \`${filter}\`` : '');
      };
      if (uniques.length) out.push('', `**القيم الفريدة:** ${uniques.map(fmt).join('، ')}`);
      out.push('');
    }
  } else if (name === 'foreign-keys') {
    out.push('<!-- widths: 20,24,20,22,14 -->', '| الجدول التابع | الأعمدة | الجدول الأصل | الأعمدة المرجعية | عند الحذف |', '|---|---|---|---|---|');
    for (const t of schema) for (const f of t.ForeignKeys)
      out.push(`| ${t.Name} | ${f.Columns.join(', ')} | ${f.ReferencedTable} | ${f.ReferencedColumns.join(', ')} | ${f.DeleteAction === 'Restrict' || f.DeleteAction === 'NoAction' ? 'Restrict' : f.DeleteAction} |`);
    out.push('', `**الإجمالي:** ${schema.reduce((n, t) => n + t.ForeignKeys.length, 0)} مفتاحًا أجنبيًا، وكلها تمنع الحذف التتابعي للحفاظ على التاريخ.`);
  } else if (name === 'checks') {
    out.push('<!-- widths: 18,26,56 -->', '| الجدول | القيد | الشرط |', '|---|---|---|');
    for (const t of schema) for (const c of t.Checks) out.push(`| ${t.Name} | ${c.Name} | \`${c.Sql.replace(/\|/g, '/')}\` |`);
    out.push('', `**الإجمالي:** ${schema.reduce((n, t) => n + t.Checks.length, 0)} قيد CHECK.`);
  }
  return out.join('\n');
}
function expandGenerated(md) {
  if (!/<!-- BEGIN generated:/.test(md)) return md;
  const schema = loadSchema();
  return md.replace(/(<!-- BEGIN generated:([\w-]+) -->)[\s\S]*?(<!-- END generated:\2 -->)/g,
    (_, begin, name, end) => `${begin}\n${generated(name, schema)}\n${end}`);
}

// ---------------------------------------------------------------- Markdown parsing
function parse(md) {
  const lines = md.replace(/\r\n/g, '\n').split('\n');
  const meta = {};
  const blocks = [];
  let i = 0;
  if (lines[0] === '---') {
    for (i = 1; i < lines.length && lines[i] !== '---'; i++) {
      const m = lines[i].match(/^(\w+):\s*(.*)$/);
      if (m) meta[m[1]] = m[2];
    }
    i++;
  }
  let pending = {};
  let para = [];
  const flush = () => { if (para.length) { blocks.push({ type: 'p', text: para.join(' ') }); para = []; } };
  for (; i < lines.length; i++) {
    const line = lines[i];
    const t = line.trim();
    if (!t) { flush(); continue; }
    let m;
    if ((m = t.match(/^<!--\s*(.*?)\s*-->$/))) {
      flush();
      const c = m[1];
      if (c === 'pagebreak') blocks.push({ type: 'pagebreak' });
      else if (c === 'landscape' || c === 'portrait') blocks.push({ type: 'orientation', value: c });
      else if (c === 'table: kv') pending.kv = true;
      else if (c === 'rotate') pending.rotate = true;
      else if ((m = c.match(/^widths:\s*([\d,\s]+)$/))) pending.widths = m[1].split(',').map(Number);
      continue;
    }
    if (t === '[[TOC]]') { flush(); blocks.push({ type: 'toc' }); continue; }
    if ((m = t.match(/^(#{1,4})\s+(.*)$/))) { flush(); blocks.push({ type: 'h', level: m[1].length, text: m[2] }); continue; }
    if (t.startsWith('```')) {
      flush();
      const code = [];
      for (i++; i < lines.length && !lines[i].trim().startsWith('```'); i++) code.push(lines[i]);
      blocks.push({ type: 'code', lines: code });
      continue;
    }
    if (t.startsWith('|')) {
      flush();
      const rows = [];
      for (; i < lines.length && lines[i].trim().startsWith('|'); i++) {
        const cells = lines[i].trim().replace(/^\|/, '').replace(/\|$/, '').split(/(?<!\\)\|/).map((c) => c.trim().replace(/\\\|/g, '|'));
        if (cells.every((c) => /^:?-{3,}:?$/.test(c))) continue;
        rows.push(cells);
      }
      i--;
      blocks.push({ type: 'table', rows, kv: !!pending.kv, widths: pending.widths });
      pending = {};
      continue;
    }
    if ((m = t.match(/^!\[(.*?)\]\((.*?)\)$/))) {
      flush();
      blocks.push({ type: 'figure', caption: m[1], src: m[2], rotate: !!pending.rotate });
      pending = {};
      continue;
    }
    if ((m = t.match(/^-\s+(.*)$/))) { flush(); pushItem(blocks, 'ul', m[1]); continue; }
    if ((m = t.match(/^\d+\.\s+(.*)$/))) { flush(); pushItem(blocks, 'ol', m[1]); continue; }
    if ((m = t.match(/^>\s?(.*)$/))) {
      flush();
      const last = blocks[blocks.length - 1];
      if (last && last.type === 'note' && !last.closed) last.text += ' ' + m[1]; else blocks.push({ type: 'note', text: m[1] });
      continue;
    }
    para.push(t);
  }
  flush();
  return { meta, blocks };
}
function pushItem(blocks, type, text) {
  const last = blocks[blocks.length - 1];
  if (last && last.type === type) last.items.push(text); else blocks.push({ type, items: [text] });
}

// ---------------------------------------------------------------- bidirectional runs
const ARABIC_LETTER = /[ء-ي٠-٩ٱ-ۓۺ-ۿﭐ-﷿ﹰ-﻿]/;
const LATIN = /[A-Za-z]/;
// Tokens: whitespace | a word (letters/digits, keeping inner . - / as in ASP.NET, 3/1/0, 1.1) | one other char.
const TOKEN = /\s+|(?:\.(?=[A-Za-z]))?[\p{L}\p{N}_#+]+(?:[.\-/][\p{L}\p{N}_#+]+)*|[^\s]/gu;
// Splits text into RTL runs (Arabic) and LTR runs (English, code-like words) so that Word orders a mixed
// sentence correctly inside an RTL paragraph. Rules for the characters in between:
//   numbers follow an adjacent English word, otherwise Arabic;
//   "(" follows the next word, ")" the previous word; other punctuation is English only between English words;
//   spaces are English only between two English runs.
function bidiSegments(text) {
  const tokens = text.match(TOKEN) || [];
  const kind = tokens.map((tk) => {
    if (/^\s+$/.test(tk)) return 'space';
    if (ARABIC_LETTER.test(tk)) return 'ar';
    if (LATIN.test(tk)) return 'lat';
    if (/^[\p{N}]/u.test(tk)) return 'num';
    if (/^[([{]$/.test(tk)) return 'open';
    if (/^[)\]}]$/.test(tk)) return 'close';
    return 'punct';
  });
  const strong = (from, step, stopAtSpace) => {
    for (let j = from; j >= 0 && j < kind.length; j += step) {
      if (kind[j] === 'ar' || kind[j] === 'lat') return kind[j];
      if (stopAtSpace && kind[j] === 'space') continue;
    }
    return null;
  };
  const resolved = kind.map((k, idx) => {
    if (k === 'ar' || k === 'lat') return k;
    const prev = strong(idx - 1, -1), next = strong(idx + 1, 1);
    if (k === 'num') return prev === 'lat' || (next === 'lat' && prev !== 'ar') ? 'lat' : 'ar';
    if (k === 'open') return next || prev || 'ar';
    if (k === 'close') return prev || next || 'ar';
    // a full stop, colon or semicolon followed by a space ends a clause: it belongs to the Arabic sentence
    if (k === 'punct' && /^[.:;]$/.test(tokens[idx]) && kind[idx + 1] === 'space') return 'ar';
    if (k === 'punct') return prev === 'lat' && next === 'lat' ? 'lat' : 'ar';
    return null;
  });
  // numbers/brackets now count as resolved; spaces join English only when both sides are English
  for (let i = 0; i < kind.length; i++) {
    if (kind[i] !== 'space') continue;
    let p = null, n = null;
    for (let j = i - 1; j >= 0 && !p; j--) if (resolved[j]) p = resolved[j];
    for (let j = i + 1; j < kind.length && !n; j++) if (resolved[j]) n = resolved[j];
    resolved[i] = p === 'lat' && n === 'lat' ? 'lat' : 'ar';
  }
  const segs = [];
  tokens.forEach((tk, idx) => {
    const rtl = resolved[idx] !== 'lat';
    const last = segs[segs.length - 1];
    if (last && last.rtl === rtl) last.text += tk; else segs.push({ text: tk, rtl });
  });
  return segs;
}
function runs(text, base = {}) {
  const out = [];
  const parts = String(text).split(/(\*\*[^*]+\*\*|`[^`]+`)/g).filter((x) => x !== '');
  for (const part of parts) {
    if (part.startsWith('`') && part.endsWith('`')) {
      out.push(new d.TextRun({ text: part.slice(1, -1), font: MONO, size: Math.max(16, (base.size || 21) - 2), color: base.color }));
      continue;
    }
    const bold = part.startsWith('**') && part.endsWith('**');
    const content = bold ? part.slice(2, -2) : part;
    // size/colour are only set when asked for, so heading styles keep control of their own look
    for (const s of bidiSegments(content)) {
      out.push(new d.TextRun({ text: s.text, font: FONT, size: base.size, bold: bold || base.bold || undefined,
        italics: base.italics, color: base.color, rightToLeft: s.rtl }));
    }
  }
  return out;
}
const P = (text, opts = {}) => new d.Paragraph({
  bidirectional: true, alignment: opts.align || d.AlignmentType.START, spacing: opts.spacing || { after: 120, line: 300 },
  keepNext: opts.keepNext, keepLines: opts.keepLines, shading: opts.shading, border: opts.border, indent: opts.indent,
  numbering: opts.numbering, heading: opts.heading, children: runs(text, opts.run || {}),
});

// ---------------------------------------------------------------- blocks to docx
function pngSize(file) {
  const b = fs.readFileSync(file);
  return { w: b.readUInt32BE(16), h: b.readUInt32BE(20), data: b };
}
function contentWidth(landscape) { return (landscape ? PAGE.h : PAGE.w) - 2 * PAGE.margin; }

function table(block, landscape) {
  const total = contentWidth(landscape);
  const cols = Math.max(...block.rows.map((r) => r.length));
  let weights = block.widths;
  if (!weights || weights.length !== cols) {
    weights = [];
    for (let c = 0; c < cols; c++) {
      const len = Math.max(...block.rows.map((r) => (r[c] || '').replace(/\*\*|`|<br>/g, '').length));
      weights.push(Math.max(8, Math.min(60, Math.sqrt(len) * 6)));
    }
  }
  const sum = weights.reduce((a, b) => a + b, 0);
  const widths = weights.map((w) => Math.floor((w / sum) * total));
  widths[widths.length - 1] += total - widths.reduce((a, b) => a + b, 0);
  const border = { style: d.BorderStyle.SINGLE, size: 4, color: COLOR.border };
  const rows = block.rows.map((cells, r) => {
    const header = !block.kv && r === 0;
    return new d.TableRow({
      tableHeader: header, cantSplit: true,
      children: widths.map((w, c) => {
        const text = cells[c] || '';
        const keyCol = block.kv && c === 0;
        const first = block.kv && r === 0;
        const fill = header || first ? COLOR.headerFill : keyCol ? COLOR.kvFill : (!block.kv && r % 2 === 0 ? COLOR.zebra : undefined);
        const runOpts = header || first ? { bold: true, color: 'FFFFFF', size: 19 } : { bold: keyCol, size: 19 };
        // key/value tables (use cases) stay on one page: every row keeps with the next one
        const keep = block.kv && r < block.rows.length - 1;
        const paras = text.split(/<br\s*\/?>/i).map((line) => P(line.trim(), { spacing: { after: 40, line: 264 }, run: runOpts, keepNext: keep, keepLines: block.kv }));
        return new d.TableCell({
          width: { size: w, type: d.WidthType.DXA },
          shading: fill ? { fill, type: d.ShadingType.CLEAR, color: 'auto' } : undefined,
          margins: { top: 70, bottom: 70, left: 110, right: 110 },
          verticalAlign: d.VerticalAlign.CENTER,
          children: paras,
        });
      }),
    });
  });
  return new d.Table({
    rows, columnWidths: widths, width: { size: total, type: d.WidthType.DXA }, layout: d.TableLayoutType.FIXED,
    visuallyRightToLeft: true,
    borders: { top: border, bottom: border, left: border, right: border, insideHorizontal: border, insideVertical: border },
  });
}

function build(mdFile, docxFile) {
  const source = path.resolve(DOCS, mdFile);
  const original = fs.readFileSync(source, 'utf8');
  const md = expandGenerated(original);
  if (md !== original) fs.writeFileSync(source, md);
  const { meta, blocks } = parse(md);
  const sections = [];
  let current = { landscape: false, children: [] };
  let figure = 0;
  let olCounter = 0;
  const headerText = meta.header || meta.title || '';
  const startSection = (landscape) => {
    if (current.children.length) sections.push(current);
    current = { landscape, children: [] };
  };

  // cover
  const title = blocks.find((b) => b.type === 'h' && b.level === 1);
  current.cover = true;
  current.children.push(new d.Paragraph({ spacing: { before: 2600 }, children: [] }));
  current.children.push(P(title ? title.text : meta.title, { align: d.AlignmentType.CENTER, spacing: { after: 240 }, run: { size: 48, bold: true, color: COLOR.heading } }));
  if (meta.subtitle) current.children.push(P(meta.subtitle, { align: d.AlignmentType.CENTER, spacing: { after: 600 }, run: { size: 26, color: COLOR.accent } }));
  const bodyBlocks = blocks.filter((b) => b !== title);

  for (const b of bodyBlocks) {
    const out = current.children;
    if (b.type !== 'ol') olCounter = olCounter; // numbering instances are per list below
    switch (b.type) {
      case 'h': {
        const level = b.level - 1;   // ## -> Heading 1
        const heading = [d.HeadingLevel.HEADING_1, d.HeadingLevel.HEADING_2, d.HeadingLevel.HEADING_3][level - 1] || d.HeadingLevel.HEADING_3;
        out.push(new d.Paragraph({ heading, bidirectional: true, alignment: d.AlignmentType.START, keepNext: true, children: runs(b.text) }));
        break;
      }
      case 'p': out.push(P(b.text, { align: d.AlignmentType.BOTH })); break;
      case 'note':
        out.push(P(b.text, { shading: { fill: COLOR.note, type: d.ShadingType.CLEAR, color: 'auto' }, spacing: { before: 80, after: 160, line: 300 },
          border: { right: { style: d.BorderStyle.SINGLE, size: 18, color: COLOR.accent, space: 6 }, left: { style: d.BorderStyle.SINGLE, size: 18, color: COLOR.accent, space: 6 } } }));
        break;
      case 'ul':
        for (const item of b.items) out.push(P(item, { numbering: { reference: 'bullets', level: 0 }, spacing: { after: 60, line: 288 } }));
        out.push(new d.Paragraph({ spacing: { after: 60 }, children: [] }));
        break;
      case 'ol': {
        olCounter++;
        for (const item of b.items) out.push(P(item, { numbering: { reference: 'numbers', level: 0, instance: olCounter }, spacing: { after: 60, line: 288 } }));
        out.push(new d.Paragraph({ spacing: { after: 60 }, children: [] }));
        break;
      }
      case 'code':
        for (const [k, line] of b.lines.entries()) out.push(new d.Paragraph({
          bidirectional: false, alignment: d.AlignmentType.LEFT, keepNext: k < b.lines.length - 1,
          spacing: { after: 0, line: 252 }, shading: { fill: COLOR.code, type: d.ShadingType.CLEAR, color: 'auto' },
          children: [new d.TextRun({ text: line.length ? line : ' ', font: MONO, size: 17 })],
        }));
        out.push(new d.Paragraph({ spacing: { after: 120 }, children: [] }));
        break;
      case 'table':
        out.push(table(b, current.landscape));
        out.push(new d.Paragraph({ spacing: { after: 160 }, children: [] }));
        break;
      case 'figure': {
        figure++;
        // <!-- rotate --> uses the quarter-turned copy made by render-diagrams.ps1, on a page of its own
        const src = b.rotate ? b.src.replace(/\.png$/, '-rotated.png') : b.src;
        if (b.rotate) out.push(new d.Paragraph({ children: [new d.PageBreak()] }));
        const img = pngSize(path.join(DOCS, src));
        const maxW = contentWidth(current.landscape) / 1440 * 96;
        const maxH = ((current.landscape ? PAGE.w : PAGE.h) - 2 * PAGE.margin - 1500) / 1440 * 96;
        const scale = Math.min(maxW / img.w, maxH / img.h);
        out.push(new d.Paragraph({ alignment: d.AlignmentType.CENTER, keepNext: true, spacing: { before: 120, after: 60 }, children: [
          new d.ImageRun({ type: 'png', data: img.data, transformation: { width: Math.round(img.w * scale), height: Math.round(img.h * scale) },
            altText: { title: b.caption, description: b.caption, name: path.basename(src) } }),
        ] }));
        out.push(P(`شكل ${figure}: ${b.caption}${b.rotate ? ' (أدِر الصفحة لقراءته)' : ''}`, { align: d.AlignmentType.CENTER, spacing: { after: 200 }, run: { size: 18, italics: true, color: COLOR.muted } }));
        if (b.rotate) out.push(new d.Paragraph({ children: [new d.PageBreak()] }));
        break;
      }
      case 'pagebreak': out.push(new d.Paragraph({ children: [new d.PageBreak()] })); break;
      case 'orientation': startSection(b.value === 'landscape'); break;
      case 'toc':
        out.push(P('فهرس المحتويات', { spacing: { before: 120, after: 200 }, run: { size: 32, bold: true, color: COLOR.heading } }));
        out.push(new d.TableOfContents('فهرس المحتويات', { hyperlink: true, headingStyleRange: '1-2' }));
        out.push(new d.Paragraph({ children: [new d.PageBreak()] }));
        break;
      default: break;
    }
  }
  sections.push(current);

  const header = () => new d.Header({ children: [P(headerText, { spacing: { after: 0 }, run: { size: 16, color: COLOR.muted },
    border: { bottom: { style: d.BorderStyle.SINGLE, size: 4, color: COLOR.border, space: 4 } } })] });
  const footer = () => new d.Footer({ children: [new d.Paragraph({ bidirectional: true, alignment: d.AlignmentType.CENTER, children: [
    new d.TextRun({ children: ['صفحة ', d.PageNumber.CURRENT, ' من ', d.PageNumber.TOTAL_PAGES], font: FONT, size: 16, color: COLOR.muted, rightToLeft: true }),
  ] })] });
  const empty = () => new d.Header({ children: [new d.Paragraph({ children: [] })] });

  const doc = new d.Document({
    creator: meta.author || 'Arena Project Team',
    title: meta.title || (title && title.text) || '',
    subject: meta.subtitle || '',
    description: 'Built from ' + mdFile,
    styles: {
      default: {
        document: { run: { font: FONT, size: 21 }, paragraph: { bidirectional: true } },
        heading1: { run: { font: FONT, size: 32, bold: true, color: COLOR.heading }, paragraph: { spacing: { before: 360, after: 160 }, keepNext: true, keepLines: true, bidirectional: true } },
        heading2: { run: { font: FONT, size: 26, bold: true, color: COLOR.accent }, paragraph: { spacing: { before: 260, after: 120 }, keepNext: true, keepLines: true, bidirectional: true } },
        heading3: { run: { font: FONT, size: 23, bold: true, color: '1F2937' }, paragraph: { spacing: { before: 200, after: 100 }, keepNext: true, keepLines: true, bidirectional: true } },
      },
      paragraphStyles: [
        { id: 'TOC1', name: 'toc 1', basedOn: 'Normal', next: 'Normal', run: { font: FONT, size: 22, bold: true }, paragraph: { bidirectional: true, spacing: { before: 120, after: 60 } } },
        { id: 'TOC2', name: 'toc 2', basedOn: 'Normal', next: 'Normal', run: { font: FONT, size: 20 }, paragraph: { bidirectional: true, spacing: { after: 40 }, indent: { right: 360 } } },
      ],
    },
    numbering: {
      config: [
        { reference: 'bullets', levels: [{ level: 0, format: d.LevelFormat.BULLET, text: '•', alignment: d.AlignmentType.START,
          style: { paragraph: { indent: { right: 540, hanging: 300 } } } }] },
        { reference: 'numbers', levels: [{ level: 0, format: d.LevelFormat.DECIMAL, text: '%1.', alignment: d.AlignmentType.START,
          style: { paragraph: { indent: { right: 540, hanging: 360 } } } }] },
      ],
    },
    sections: sections.map((s) => ({
      properties: {
        titlePage: !!s.cover,
        page: { size: { width: PAGE.w, height: PAGE.h, orientation: s.landscape ? d.PageOrientation.LANDSCAPE : d.PageOrientation.PORTRAIT },
          margin: { top: PAGE.margin, bottom: PAGE.margin, left: PAGE.margin, right: PAGE.margin, header: 567, footer: 567 } },
      },
      headers: s.cover ? { default: header(), first: empty() } : { default: header() },
      footers: s.cover ? { default: footer(), first: new d.Footer({ children: [new d.Paragraph({ children: [] })] }) } : { default: footer() },
      children: s.children,
    })),
  });
  return d.Packer.toBuffer(doc).then((buf) => {
    fs.writeFileSync(path.resolve(DOCS, docxFile), buf);
    console.log(`${docxFile}: ${blocks.length} blocks, ${figure} figures, ${sections.length} sections`);
  });
}

const only = process.argv.slice(2);
// `node build-docs.js some.md out.docx` builds an arbitrary file (paths relative to docs/), e.g. for experiments
if (only.length === 2 && only[0].endsWith('.md') && only[1].endsWith('.docx')) {
  build(only[0], only[1]).catch((e) => { console.error(e); process.exit(1); });
  return;
}
const jobs = [['01_System_Analysis.md', '01_System_Analysis.docx'], ['02_Database_Design_and_Mapping.md', '02_Database_Design_and_Mapping.docx']]
  .filter(([md]) => !only.length || only.some((o) => md.startsWith(o)));
(async () => { for (const [md, docx] of jobs) await build(md, docx); })().catch((e) => { console.error(e); process.exit(1); });
