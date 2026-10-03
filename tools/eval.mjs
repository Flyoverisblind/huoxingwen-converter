// 评测：简体 -> 火星文 -> 反查回简体 的还原准确率（对比原站算法与智能消歧）
import fs from 'node:fs';
import path from 'node:path';
import { loadEngine, toHx, toJt, toJtSmart } from './engine.mjs';

const E = loadEngine();
const ROOT = 'C:/Users/W/Downloads/火星文转换';

// 语料：从 jieba 词典中抽取常用多字词
function sampleWords(n, minFreq, seed) {
  const lines = fs.readFileSync(path.join(ROOT, '_ref', 'jieba_dict.txt'), 'utf8').split('\n');
  const list = [];
  let x = seed;
  const rnd = () => (x = (x * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff;
  for (const line of lines) {
    const sp = line.indexOf(' ');
    if (sp <= 0) continue;
    const w = line.slice(0, sp), f = parseInt(line.slice(sp + 1), 10);
    if (!(f >= minFreq)) continue;
    const cs = Array.from(w);
    if (cs.length < 2 || cs.length > 6) continue;
    if (cs.some((c) => !/[\u4e00-\u9fff]/.test(c))) continue;
    list.push(w);
  }
  // 随机抽样
  for (let i = list.length - 1; i > 0; i--) { const j = Math.floor(rnd() * (i + 1)); [list[i], list[j]] = [list[j], list[i]]; }
  return list.slice(0, n);
}

const words = sampleWords(4000, 800, 20240503);
console.log('评测词条:', words.length);

function evaluate(label, reverse) {
  let okChar = 0, totChar = 0, okWord = 0;
  for (const w of words) {
    const hx = toHx(E, w);
    const back = reverse(hx);
    if (back === w) okWord++;
    const a = Array.from(w), b = Array.from(back);
    for (let i = 0; i < a.length; i++) { totChar++; if (a[i] === b[i]) okChar++; }
  }
  console.log(label.padEnd(34), '字准确率', (okChar / totChar * 100).toFixed(2) + '%', ' 整词准确率', (okWord / words.length * 100).toFixed(2) + '%');
  return okChar / totChar;
}

evaluate('原站算法(首个匹配)', (s) => toJt(E, s));
evaluate('智能-仅字频(1/8)', (s) => toJtSmart(E, s, { wChar: 1 / 8, wPair: 0 }));
for (const [wc, wp] of [[1 / 8, 1 / 2], [1 / 8, 1 / 4], [0, 1 / 2], [1 / 20, 1 / 2], [1 / 8, 1], [1 / 4, 1 / 2]]) {
  evaluate(`智能-字频${wc.toFixed(3)}+二元${wp}`, (s) => toJtSmart(E, s, { wChar: wc, wPair: wp }));
}

// 句子级
const sentences = [];
for (let i = 0; i < 200; i++) {
  const parts = [];
  for (let k = 0; k < 8; k++) parts.push(words[(i * 8 + k) % words.length]);
  sentences.push(parts.join('，') + '。');
}
console.log('\n句子级评测:', sentences.length, '句');
function evaluateSent(label, reverse) {
  let ok = 0, tot = 0;
  for (const s of sentences) {
    const back = reverse(toHx(E, s));
    const a = Array.from(s), b = Array.from(back);
    for (let i = 0; i < a.length; i++) { tot++; if (a[i] === b[i]) ok++; }
  }
  console.log(label.padEnd(34), '字准确率', (ok / tot * 100).toFixed(2) + '%');
}
evaluateSent('原站算法(首个匹配)', (s) => toJt(E, s));
evaluateSent('智能-字频1/8+二元1/2', (s) => toJtSmart(E, s, { wChar: 1 / 8, wPair: 1 / 2 }));

console.log('\n--- 样例 ---');
for (const s of ['爸爸', '我是中国人', '我爱你', '生日快乐', '今天天气不错', '妈妈', '你好吗', '谢谢', '这个周末去公园散步，顺便买些水果回来。']) {
  const h = toHx(E, s);
  console.log(s, '->', h, '-> 原站:', toJt(E, h), '| 智能:', toJtSmart(E, h));
}
