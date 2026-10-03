// 验证：编译后的 exe（-cli 模式）与 Node 参考实现逐字符对比
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { loadEngine, toHx, toJt, toFt, toJtSmart } from './engine.mjs';

const ROOT = 'C:/Users/W/Downloads/火星文转换';
const TMP = path.join(ROOT, 'build', '_test');
fs.mkdirSync(TMP, { recursive: true });

const EXE = path.join(ROOT, 'build', '火星文转换器.exe');
const E = loadEngine();
const enc = 'utf8';

function runCli(mode, text) {
  const inFile = path.join(TMP, 'in.txt');
  const outFile = path.join(TMP, 'out.txt');
  fs.writeFileSync(inFile, text, enc);
  execFileSync(EXE, ['-cli', mode, inFile, outFile], { stdio: 'inherit' });
  return fs.readFileSync(outFile, enc);
}

// 语料
const jtAll = E.jt.join('');
const srcAll = [...new Set([...E.hx, ...E.ft, ...E.jt])].join('');
const sentences = [];
{
  const lines = fs.readFileSync(path.join(ROOT, '_ref', 'jieba_dict.txt'), 'utf8').split('\n');
  const words = [];
  let x = 987654321;
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
  for (let i = 0; i < 400; i++) {
    const parts = [];
    for (let k = 0; k < 10; k++) parts.push(words[Math.floor(rnd() * words.length)]);
    sentences.push(parts.join('，') + '。');
  }
}
const mixed = '你好，hello 123 😀 ～《火星文》T恤 ABC！\n\t第二行：测试\t换行与标点。';

const cases = [
  ['全部简体表(3754字)', jtAll],
  ['全部源字符(6618字)', srcAll],
  ['随机常用句×400', sentences.join('\n')],
  ['混排文本', mixed]
];

let fail = 0;
function check(label, mode, input, expect) {
  const got = runCli(mode, input);
  if (got === expect) {
    console.log('  ✓', label.padEnd(22), mode.padEnd(6), '长度', got.length);
  } else {
    fail++;
    const a = Array.from(expect), b = Array.from(got);
    let i = 0;
    while (i < a.length && i < b.length && a[i] === b[i]) i++;
    console.log('  ✗', label, mode, '首个差异位置', i, '期望', JSON.stringify(a[i]), '实际', JSON.stringify(b[i]),
      '上下文 期望', JSON.stringify(a.slice(Math.max(0, i - 5), i + 5).join('')), '实际', JSON.stringify(b.slice(Math.max(0, i - 5), i + 5).join('')));
  }
}

console.log('对比 exe 与参考实现：');
for (const [label, input] of cases) {
  check(label, 'hx', input, toHx(E, input));
  check(label, 'cn', input, toJt(E, input));
  check(label, 'smart', input, toJtSmart(E, input));
  check(label, 'ft', input, toFt(E, input));
}

console.log('\n往返自检（exe 内部统计）：');
const rt = runCli('roundtrip', jtAll);
process.stdout.write(rt);

console.log(fail === 0 ? '\n全部一致 ✅' : `\n有 ${fail} 项不一致 ❌`);
process.exitCode = fail === 0 ? 0 : 1;
