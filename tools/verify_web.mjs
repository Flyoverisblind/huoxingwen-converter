// 验证网页版：直接抽取 build/火星文转换器.html 里的内联数据与引擎代码，与参考实现对比
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { loadEngine, toHx, toJt, toFt, toJtSmart } from './engine.mjs';

const ROOT = 'C:/Users/W/Downloads/火星文转换';
const htmlFile = path.join(ROOT, 'build', '火星文转换器.html');
const html = fs.readFileSync(htmlFile, 'utf8');

function block(id) {
  const re = new RegExp('<script type="text/plain" id="' + id + '">([\\s\\S]*?)</script>');
  const m = html.match(re);
  if (!m) throw new Error('未找到内联数据块: ' + id);
  return m[1];
}

const dictLines = block('dictData').replace(/\r/g, '').replace(/\n+$/, '').split('\n');
const smartText = block('smartData').replace(/\r/g, '');

// 抽取 HTML 中的引擎代码（第一个包含 makeEngine 的普通 script 块）
const scripts = [...html.matchAll(/<script>([\s\S]*?)<\/script>/g)].map((m) => m[1]);
const engineSrc = scripts.find((s) => s.includes('function makeEngine'));
if (!engineSrc) throw new Error('未找到引擎代码');

const sandbox = { module: { exports: {} }, console };
vm.createContext(sandbox);
vm.runInContext(engineSrc, sandbox);
const makeEngine = sandbox.makeEngine || sandbox.module.exports.makeEngine;
if (typeof makeEngine !== 'function') throw new Error('引擎未导出 makeEngine');

const W = makeEngine(dictLines[0], dictLines[1], dictLines[2], smartText);
const E = loadEngine();

console.log('网页版引擎表长:', W.tableSize, '（参考:', E.jt.length, '）');
if (dictLines[0] !== E.jt.join('') || dictLines[1] !== E.ft.join('') || dictLines[2] !== E.hx.join('')) {
  throw new Error('网页版对照表与参考不一致');
}

// 语料
const words = [];
{
  const lines = fs.readFileSync(path.join(ROOT, '_ref', 'jieba_dict.txt'), 'utf8').split('\n');
  let x = 24681357;
  const rnd = () => (x = (x * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff;
  for (const line of lines) {
    const sp = line.indexOf(' ');
    if (sp <= 0) continue;
    const w = line.slice(0, sp), f = parseInt(line.slice(sp + 1), 10);
    if (f < 500) continue;
    const cs = Array.from(w);
    if (cs.length < 2 || cs.length > 4 || cs.some((c) => !/[\u4e00-\u9fff]/.test(c))) continue;
    words.push(w);
  }
  for (let i = words.length - 1; i > 0; i--) { const j = Math.floor(rnd() * (i + 1)); [words[i], words[j]] = [words[j], words[i]]; }
}

const cases = [
  ['全部简体表', E.jt.join('')],
  ['全部源字符', [...new Set([...E.hx, ...E.ft, ...E.jt])].join('')],
  ['随机常用词×3000', words.slice(0, 3000).join('，')],
  ['混排文本', '你好，hello 123 ～《火星文》T恤 ABC！\n第二行：测试\t换行与标点。']
];

let fail = 0;
function cmp(label, mode, got, expect) {
  if (got === expect) { console.log('  ✓', label.padEnd(18), mode); return; }
  fail++;
  const a = Array.from(expect), b = Array.from(got);
  let i = 0; while (i < a.length && i < b.length && a[i] === b[i]) i++;
  console.log('  ✗', label, mode, '位置', i, '期望', JSON.stringify(a[i]), '实际', JSON.stringify(b[i]),
    JSON.stringify(a.slice(Math.max(0, i - 4), i + 4).join('')), 'vs', JSON.stringify(b.slice(Math.max(0, i - 4), i + 4).join('')));
}

console.log('\n网页版引擎 vs 参考实现:');
for (const [label, input] of cases) {
  cmp(label, 'hx', W.toMartian(input), toHx(E, input));
  cmp(label, 'cn', W.toSimplified(input), toJt(E, input));
  cmp(label, 'smart', W.toSimplifiedSmart(input), toJtSmart(E, input));
  cmp(label, 'ft', W.toTraditional(input), toFt(E, input));
}

// 反向准确率（与桌面版同一评测口径）
let okPlain = 0, okSmart = 0, tot = 0;
for (const w of words.slice(0, 4000)) {
  const h = W.toMartian(w);
  if (W.toSimplified(h) === w) okPlain++;
  if (W.toSimplifiedSmart(h) === w) okSmart++;
  tot++;
}
console.log('\n网页版反查准确率（' + tot + ' 个常用词）: 首个匹配 ' + (okPlain / tot * 100).toFixed(2) + '%  智能消歧 ' + (okSmart / tot * 100).toFixed(2) + '%');
console.log(fail === 0 ? '\n网页版全部一致 ✅' : '\n有 ' + fail + ' 项不一致 ❌');
process.exitCode = fail === 0 ? 0 : 1;
