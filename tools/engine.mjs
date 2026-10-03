// 转换引擎原型（Node 版）：与原站语义一致 + 可选智能消歧
// 该文件同时作为 C# 实现的对照基准（oracle）
import fs from 'node:fs';
import zlib from 'node:zlib';
import path from 'node:path';

const ROOT = 'C:/Users/W/Downloads/火星文转换';
const OUT = path.join(ROOT, 'build');

export function loadEngine() {
  const [jtS, ftS, hxS] = fs.readFileSync(path.join(OUT, 'dict.tsv'), 'utf8').replace(/\n$/, '').split('\n');
  const jt = Array.from(jtS), ft = Array.from(ftS), hx = Array.from(hxS);

  const jtIdx = new Map(), ftIdx = new Map();
  jt.forEach((c, i) => { if (!jtIdx.has(c)) jtIdx.set(c, i); });
  ft.forEach((c, i) => { if (!ftIdx.has(c)) ftIdx.set(c, i); });

  const hxCand = new Map(); // 火星文字 -> [汉字候选, 按表序]
  hx.forEach((c, i) => {
    if (!hxCand.has(c)) hxCand.set(c, []);
    const a = hxCand.get(c);
    if (!a.includes(jt[i])) a.push(jt[i]);
  });

  // 智能消歧数据
  const smartRaw = zlib.gunzipSync(fs.readFileSync(path.join(OUT, 'smart.gz'))).toString('utf8');
  const charFreq = new Map(), pairFreq = new Map();
  let mode = null;
  for (const line of smartRaw.split('\n')) {
    if (line === '#FREQ') { mode = 'f'; continue; }
    if (line === '#PAIR') { mode = 'p'; continue; }
    if (!line) continue;
    const t = line.indexOf('\t');
    if (mode === 'f') charFreq.set(line.slice(0, t), Number(line.slice(t + 1)));
    else if (mode === 'p') pairFreq.set(line.slice(0, t), Number(line.slice(t + 1)));
  }

  // 火星文/繁体 -> 简体候选；顺序与原站一致：繁体表 -> 火星文表 -> 原字
  const candCache = new Map();
  function candidates(ch) {
    if (candCache.has(ch)) return candCache.get(ch);
    const list = [];
    const fi = ftIdx.get(ch);
    if (fi !== undefined) list.push(jt[fi]);
    const hc = hxCand.get(ch);
    if (hc) for (const c of hc) if (!list.includes(c)) list.push(c);
    if (jtIdx.has(ch) && !list.includes(ch)) list.push(ch);
    candCache.set(ch, list);
    return list;
  }
  // 智能候选序：按字频降序（字频相同保持原站顺序）
  function candidatesSmart(ch) {
    const base = candidates(ch);
    if (base.length < 2) return base;
    return base.map((c, i) => ({ c, f: charFreq.get(c) || 0, i }))
      .sort((a, b) => (b.f - a.f) || (a.i - b.i))
      .map((s) => s.c);
  }

  return { jt, ft, hx, jtIdx, ftIdx, hxCand, charFreq, pairFreq, candidates, candidatesSmart };
}

// ---- 与原站完全一致的方向 ----
export function toHx(E, s) { // 简体/繁体 -> 火星文 (qqgo)
  let out = '';
  for (const ch of s) {
    let i = E.jtIdx.get(ch);
    if (i === undefined) i = E.ftIdx.get(ch);
    out += i === undefined ? ch : E.hx[i];
  }
  return out;
}
export function toJt(E, s) { // 火星文/繁体 -> 简体 (jtgo)
  let out = '';
  for (const ch of s) {
    const i = E.ftIdx.get(ch);
    if (i !== undefined) { out += E.jt[i]; continue; }
    const h = E.hxCand.get(ch);
    if (h) { out += h[0]; continue; }
    out += ch;
  }
  return out;
}
export function toFt(E, s) { // 简体/火星文 -> 繁体 (ftgo)
  let out = '';
  for (const ch of s) {
    let i = E.jtIdx.get(ch);
    if (i === undefined) i = E.hxCand.has(ch) ? E.hx.indexOf(ch) : undefined;
    out += i === undefined ? ch : E.ft[i];
  }
  return out;
}

// ---- 智能反查：字频 + 二元组打分的 DP ----
export function toJtSmart(E, s, opts = {}) {
  const wChar = opts.wChar ?? 1 / 4;
  const wPair = opts.wPair ?? 1 / 2;
  const cap = opts.pairCap ?? Infinity;
  const chars = Array.from(s);
  const cands = chars.map((ch) => {
    const list = E.candidatesSmart(ch);
    return list.length ? list : [ch];
  });
  const n = chars.length;
  if (n === 0) return '';
  const score = (c) => Math.log1p(E.charFreq.get(c) || 0) * wChar;
  const pair = (a, b) => {
    const w = E.pairFreq.get(a + b);
    if (!w) return 0;
    return Math.min(Math.log1p(w) * wPair, cap);
  };

  let prev = cands[0].map((c) => ({ c, s: score(c) }));
  const back = [prev.map(() => -1)];
  for (let i = 1; i < n; i++) {
    const cur = [];
    const bk = [];
    for (let j = 0; j < cands[i].length; j++) {
      const c = cands[i][j];
      let best = -Infinity, bi = 0;
      for (let k = 0; k < prev.length; k++) {
        const v = prev[k].s + pair(prev[k].c, c);
        if (v > best) { best = v; bi = k; }
      }
      cur.push({ c, s: best + score(c) });
      bk.push(bi);
    }
    back.push(bk);
    prev = cur;
  }
  let bi = 0;
  for (let j = 1; j < prev.length; j++) if (prev[j].s > prev[bi].s) bi = j;
  const out = new Array(n);
  for (let i = n - 1; i >= 0; i--) {
    out[i] = cands[i][bi];
    bi = back[i][bi];
  }
  return out.join('');
}
