// 调参：用“无分隔符”的真实连写语料评测，找出更稳的字频/词组权重
import fs from 'node:fs';
import path from 'node:path';
import { loadEngine, toHx, toJt, toJtSmart } from './engine.mjs';

const ROOT = 'C:/Users/W/Downloads/火星文转换';
const E = loadEngine();

const lines = fs.readFileSync(path.join(ROOT, '_ref', 'jieba_dict.txt'), 'utf8').split('\n');
const pool = [];
let x = 13572468;
const rnd = () => (x = (x * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff;
for (const line of lines) {
  const sp = line.indexOf(' ');
  if (sp <= 0) continue;
  const w = line.slice(0, sp), f = parseInt(line.slice(sp + 1), 10);
  if (f < 2000) continue;
  const cs = Array.from(w);
  if (cs.length < 1 || cs.length > 4 || cs.some((c) => !/[\u4e00-\u9fff]/.test(c))) continue;
  pool.push(w);
}
// 连写句子（词之间不加标点，模拟真实文本，考察跨词歧义）
const sentences = [];
for (let i = 0; i < 600; i++) {
  let s = '';
  const n = 6 + Math.floor(rnd() * 8);
  for (let k = 0; k < n; k++) s += pool[Math.floor(rnd() * pool.length)];
  if (s.length >= 10) sentences.push(s);
}
console.log('连写句子:', sentences.length, '句，平均', Math.round(sentences.reduce((a, s) => a + s.length, 0) / sentences.length), '字');

function evalSent(reverse) {
  let ok = 0, tot = 0, badSent = 0;
  for (const s of sentences) {
    const back = reverse(toHx(E, s));
    let bad = false;
    const a = Array.from(s), b = Array.from(back);
    for (let i = 0; i < a.length; i++) { tot++; if (a[i] === b[i]) ok++; else bad = true; }
    if (bad) badSent++;
  }
  return { acc: ok / tot * 100, badSent, total: sentences.length };
}

console.log('\n首个匹配:', JSON.stringify(evalSent((s) => toJt(E, s))));
const combos = [
  [1 / 8, 1 / 2, Infinity], [1 / 2, 1 / 2, Infinity], [1, 1 / 2, Infinity],
  [1 / 2, 1 / 2, 2], [1 / 2, 1 / 2, 1.5], [1 / 2, 1 / 2, 1], [1, 1 / 2, 2], [1, 1 / 2, 1.5],
  [3 / 4, 1 / 2, 2], [1 / 4, 1 / 2, 1], [1 / 2, 1 / 4, 1], [1, 1 / 4, 1.5]
];
for (const [wc, wp, cap] of combos) {
  const r = evalSent((s) => toJtSmart(E, s, { wChar: wc, wPair: wp, pairCap: cap }));
  console.log(`字频${wc.toFixed(3)} 二元${wp} 上限${cap}`.padEnd(28), '字准确率', r.acc.toFixed(2) + '%', ' 错句', r.badSent + '/' + r.total);
}

console.log('\n关键样例:');
for (const s of ['今天的天气真不错', '这是我爸爸和他的朋友', '他的手机在哪里', '我知道你是谁', '爸爸去哪儿了']) {
  const h = toHx(E, s);
  console.log(s.padEnd(12), '->', '首个:', toJt(E, h).padEnd(12), '| A:', toJtSmart(E, h).padEnd(12),
    '| B(cap1.5):', toJtSmart(E, h, { wChar: 1 / 2, wPair: 1 / 2, pairCap: 1.5 }).padEnd(12),
    '| C(cap1):', toJtSmart(E, h, { wChar: 1, wPair: 1 / 2, pairCap: 1 }));
}
