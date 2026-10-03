// 生成免安装网页版：build/火星文转换器.html（单文件、离线可用）
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';

const ROOT = 'C:/Users/W/Downloads/火星文转换';
const OUT = path.join(ROOT, 'build');
const tpl = fs.readFileSync(path.join(ROOT, 'tools', 'web', 'index.template.html'), 'utf8');
const engineJs = fs.readFileSync(path.join(ROOT, 'tools', 'web', 'engine.js'), 'utf8');
const dict = fs.readFileSync(path.join(OUT, 'dict.tsv'), 'utf8').replace(/\r/g, '').trimEnd('\n');
const smart = zlib.gunzipSync(fs.readFileSync(path.join(OUT, 'smart.gz'))).toString('utf8').trimEnd('\n');

// text/plain 脚本块内不能出现 </script
for (const [name, s] of [['dict', dict], ['smart', smart], ['engine', engineJs]]) {
  if (/<\/script/i.test(s)) throw new Error(name + ' 含有 </script，无法内联');
}

const html = tpl
  .replace('{{ENGINE}}', () => engineJs)
  .replace('{{DICT}}', () => dict)
  .replace('{{SMART}}', () => smart);

const file = path.join(OUT, '火星文转换器.html');
fs.writeFileSync(file, html, 'utf8');
console.log('已生成', file, (fs.statSync(file).size / 1024).toFixed(0) + ' KB');
