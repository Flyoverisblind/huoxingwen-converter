import { loadEngine, toHx, toJtSmart, toJt } from './engine.mjs';
const E = loadEngine();
const s = '他的手机在哪里';
const h = toHx(E, s);
console.log('原文', s, '火星文', h);
console.log('字频：的 =', E.charFreq.get('的'), ' 地 =', E.charFreq.get('地'), ' 在 =', E.charFreq.get('在'), ' 茬 =', E.charFreq.get('茬'));
for (const ch of h) {
  const c = E.candidatesSmart(ch);
  console.log(' ', ch, '->', c.map((x) => x + '(' + (E.charFreq.get(x) || 0) + ')').join(' '));
}
const chars = Array.from(h);
for (let i = 0; i + 1 < chars.length; i++) {
  const A = E.candidatesSmart(chars[i]), B = E.candidatesSmart(chars[i + 1]);
  for (const a of A) for (const b of B) {
    const w = E.pairFreq.get(a + b);
    if (w) console.log('  二元组', a + b, '权重', w, '得分', (Math.log1p(w) * 0.5).toFixed(2));
  }
}
console.log('结果:', toJtSmart(E, h), '| 首个匹配:', toJt(E, h));
