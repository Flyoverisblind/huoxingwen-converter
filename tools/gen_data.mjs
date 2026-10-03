// 生成 app 所需的数据资源：dict.tsv（三张对照表）与 smart.gz（智能消歧统计表）
import fs from 'node:fs';
import zlib from 'node:zlib';
import path from 'node:path';

const ROOT = 'C:/Users/W/Downloads/火星文转换';
const REF = path.join(ROOT, '_ref');
const OUT = path.join(ROOT, 'build');
fs.mkdirSync(OUT, { recursive: true });

const T = JSON.parse(fs.readFileSync(path.join(REF, 'tables.json'), 'utf8'));
const { jt, ft, hx } = T;

// ---- 1. 对照表 ----
fs.writeFileSync(path.join(OUT, 'dict.tsv'), jt.join('') + '\n' + ft.join('') + '\n' + hx.join('') + '\n', 'utf8');

// ---- 2. 反查候选（火星文/繁体 -> 简体候选，含全部来源）----
const jtIdx = new Map(), ftIdx = new Map();
jt.forEach((c, i) => { if (!jtIdx.has(c)) jtIdx.set(c, i); });
ft.forEach((c, i) => { if (!ftIdx.has(c)) ftIdx.set(c, i); });
const hxCand = new Map();
hx.forEach((c, i) => {
  if (!hxCand.has(c)) hxCand.set(c, []);
  const a = hxCand.get(c);
  if (!a.includes(jt[i])) a.push(jt[i]);
});

const merged = new Map();
function mergedCandidates(ch) {
  if (merged.has(ch)) return merged.get(ch);
  const list = [];
  const fi = ftIdx.get(ch);
  if (fi !== undefined) list.push(jt[fi]);
  const hc = hxCand.get(ch);
  if (hc) for (const c of hc) if (!list.includes(c)) list.push(c);
  if (jtIdx.has(ch) && !list.includes(ch)) list.push(ch);
  merged.set(ch, list);
  return list;
}
// 所有可能出现在输入里的字符
const sourceChars = new Set([...hx, ...ft, ...jt]);
const candidateSet = new Set();
let ambCount = 0;
for (const ch of sourceChars) {
  const l = mergedCandidates(ch);
  if (l.length > 1) { ambCount++; l.forEach((c) => candidateSet.add(c)); }
}
console.log('输入字符种类:', sourceChars.size, '多候选字符:', ambCount, '参与统计的候选汉字:', candidateSet.size);

// 原站算法一致性自检
const jtgo = (s) => Array.from(s).map((ch) => {
  const fi = ftIdx.get(ch);
  if (fi !== undefined) return jt[fi];
  const h = hxCand.get(ch);
  return h ? h[0] : ch;
}).join('');
let same = 0, diff = 0;
for (const ch of sourceChars) {
  if (jtgo(ch) === mergedCandidates(ch)[0]) same++;
  else { diff++; if (diff < 6) console.log('  顺序差异:', ch, jtgo(ch), mergedCandidates(ch)[0]); }
}
console.log('原站顺序与合并顺序一致:', same, '不一致:', diff);

// ---- 3. 从 jieba 词典统计：字频 + 二元组 ----
const dictRaw = fs.readFileSync(path.join(REF, 'jieba_dict.txt'), 'utf8');
const isHan = (c) => /[\u3400-\u4dbf\u4e00-\u9fff\uf900-\ufaff]/.test(c);

const charFreq = new Map();
const charSolo = new Map();   // 作为独立单词出现的频次
const charInWord = new Map(); // 出现在多字词内部的频次
const pairFreq = new Map();
let wordCount = 0;
for (const line of dictRaw.split('\n')) {
  const sp = line.indexOf(' ');
  if (sp <= 0) continue;
  const word = line.slice(0, sp);
  const freq = parseInt(line.slice(sp + 1), 10);
  if (!Number.isFinite(freq)) continue;
  const chars = Array.from(word);
  if (chars.length < 1 || chars.some((c) => !isHan(c))) continue;
  wordCount++;
  if (chars.length === 1) {
    // 单字词（的/了/在…）本身就是该字的语料频次，必须计入
    if (candidateSet.has(chars[0])) charSolo.set(chars[0], (charSolo.get(chars[0]) || 0) + freq);
  } else {
    for (const c of chars) {
      if (candidateSet.has(c)) charInWord.set(c, (charInWord.get(c) || 0) + Math.min(freq, 20000));
    }
    for (let i = 0; i + 1 < chars.length; i++) {
      const a = chars[i], b = chars[i + 1];
      if (!candidateSet.has(a) && !candidateSet.has(b)) continue;
      const w = Math.min(freq, 20000);
      const key = a + b;
      if ((pairFreq.get(key) || 0) < w) pairFreq.set(key, w);
    }
  }
}
// 字频：优先用“作为独立单词”的频次，没有单字词条时退化为词内出现频次
for (const c of candidateSet) {
  const s = charSolo.get(c);
  charFreq.set(c, s !== undefined ? s : (charInWord.get(c) || 0));
}
console.log('统计用词条:', wordCount, '字频条目:', charFreq.size, '二元组:', pairFreq.size,
  '（其中单字词条', charSolo.size, '个）');

const freqLines = [...charFreq].map(([c, f]) => c + '\t' + f);
const pairLines = [...pairFreq].map(([k, v]) => k + '\t' + v);
const smart = '#FREQ\n' + freqLines.join('\n') + '\n#PAIR\n' + pairLines.join('\n') + '\n';
const gz = zlib.gzipSync(Buffer.from(smart, 'utf8'), { level: 9 });
fs.writeFileSync(path.join(OUT, 'smart.gz'), gz);
console.log('smart 原始', smart.length, 'gzip', gz.length);
