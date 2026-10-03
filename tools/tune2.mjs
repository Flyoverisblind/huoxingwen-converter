// 字频代理方案对比：单字词频 vs 词内出现频次
import fs from 'node:fs';
import path from 'node:path';
import { loadEngine, toHx, toJt, toJtSmart } from './engine.mjs';

const ROOT = 'C:/Users/W/Downloads/火星文转换';
const E = loadEngine();
const isHan = (c) => /[\u3400-\u4dbf\u4e00-\u9fff\uf900-\ufaff]/.test(c);
// 候选字集合 = 现有字频表的键
const cands = new Set(E.charFreq.keys());

const solo = new Map(), comp = new Map();
for (const line of fs.readFileSync(path.join(ROOT, '_ref', 'jieba_dict.txt'), 'utf8').split('\n')) {
  const sp = line.indexOf(' ');
  if (sp <= 0) continue;
  const w = line.slice(0, sp), f = parseInt(line.slice(sp + 1), 10);
  if (!Number.isFinite(f)) continue;
  const cs = Array.from(w);
  if (!cs.length || cs.some((c) => !isHan(c))) continue;
  if (cs.length === 1) {
    if (cands.has(cs[0])) solo.set(cs[0], (solo.get(cs[0]) || 0) + f);
  } else {
    for (const c of cs) if (cands.has(c)) comp.set(c, (comp.get(c) || 0) + Math.min(f, 20000));
  }
}
console.log('单字词条覆盖候选字:', solo.size, '/', cands.size);

const variants = {
  '现方案(仅词内,cap20000)': (c) => comp.get(c) || 0,
  '单字词频优先': (c) => (solo.get(c) || 0) + 0.1 * (comp.get(c) || 0),
  '单字+词内0.02': (c) => (solo.get(c) || 0) + 0.02 * (comp.get(c) || 0),
  '仅单字(缺则词内)': (c) => (solo.get(c) || 0) || (comp.get(c) || 0),
  '单字+词内0.3': (c) => (solo.get(c) || 0) + 0.3 * (comp.get(c) || 0)
};

const pool = [];
let x = 13572468;
const rnd = () => (x = (x * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff;
for (const line of fs.readFileSync(path.join(ROOT, '_ref', 'jieba_dict.txt'), 'utf8').split('\n')) {
  const sp = line.indexOf(' ');
  if (sp <= 0) continue;
  const w = line.slice(0, sp), f = parseInt(line.slice(sp + 1), 10);
  if (f < 2000) continue;
  const cs = Array.from(w);
  if (cs.length < 1 || cs.length > 4 || cs.some((c) => !isHan(c))) continue;
  pool.push(w);
}
const sentences = [];
for (let i = 0; i < 600; i++) {
  let s = '';
  const n = 6 + Math.floor(rnd() * 8);
  for (let k = 0; k < n; k++) s += pool[Math.floor(rnd() * pool.length)];
  if (s.length >= 10) sentences.push(s);
}
// 词级语料（同一批词，单独评测）
const wordList = [];
for (let i = 0; i < 4000; i++) wordList.push(pool[Math.floor(rnd() * pool.length)]);

function evalSent(reverse) {
  let ok = 0, tot = 0, bad = 0;
  for (const s of sentences) {
    const back = reverse(toHx(E, s));
    let b = false;
    const a = Array.from(s), c = Array.from(back);
    for (let i = 0; i < a.length; i++) { tot++; if (a[i] === c[i]) ok++; else b = true; }
    if (b) bad++;
  }
  return { acc: ok / tot * 100, bad, total: sentences.length };
}
function evalWords(reverse) {
  let ok = 0;
  for (const w of wordList) if (reverse(toHx(E, w)) === w) ok++;
  return ok / wordList.length * 100;
}

const original = new Map(E.charFreq);
console.log('\n基准(现方案) 句子字准确率', evalSent((s) => toJtSmart(E, s)).acc.toFixed(2) + '%', ' 词准确率', evalWords((s) => toJtSmart(E, s)).toFixed(2) + '%');

const samples = ['今天的天气真不错', '他的手机在哪里', '这是我爸爸和他的朋友', '爸爸去哪儿了', '我知道你是谁'];
for (const [name, fn] of Object.entries(variants)) {
  for (const c of cands) E.charFreq.set(c, fn(c));
  for (const wc of [1 / 8, 1 / 4, 1 / 2]) {
    const r = evalSent((s) => toJtSmart(E, s, { wChar: wc, wPair: 1 / 2 }));
    const wr = evalWords((s) => toJtSmart(E, s, { wChar: wc, wPair: 1 / 2 }));
    console.log(('\n' + name + ' 字频权重' + wc).padEnd(34), '句子字准确率', r.acc.toFixed(2) + '%', ' 错句', r.bad + '/' + r.total, ' 词准确率', wr.toFixed(2) + '%');
  }
  for (const c of cands) E.charFreq.set(c, fn(c));
  console.log('  样例:', samples.map((s) => toJtSmart(E, s, { wChar: 1 / 4 }).slice(0, 8)).join(' | '));
}
for (const c of cands) E.charFreq.set(c, original.get(c));
