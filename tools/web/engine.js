/* 火星文转换引擎（浏览器版，纯函数、无 DOM 依赖）
   与桌面版 C# 实现使用同一套数据与同一套算法：
   - 中文 -> 火星文、火星文 -> 中文（首个匹配）与在线工具结果一致
   - 火星文 -> 中文（智能消歧）：字频 + 常用词二元组统计 + 动态规划 */
if (typeof Array.from !== 'function') {
  // 兼容旧版 Android WebView
  Array.from = function (s) { return typeof s === 'string' ? s.split('') : Array.prototype.slice.call(s); };
}
function makeEngine(jtText, ftText, hxText, smartText) {
  var JT = Array.from(jtText), FT = Array.from(ftText), HX = Array.from(hxText);

  var jtIdx = new Map(), ftIdx = new Map(), hxFirst = new Map(), hxCand = new Map();
  JT.forEach(function (c, i) { if (!jtIdx.has(c)) jtIdx.set(c, i); });
  FT.forEach(function (c, i) { if (!ftIdx.has(c)) ftIdx.set(c, i); });
  HX.forEach(function (c, i) {
    if (!hxFirst.has(c)) hxFirst.set(c, i);
    if (!hxCand.has(c)) hxCand.set(c, []);
    var a = hxCand.get(c);
    if (a.indexOf(JT[i]) < 0) a.push(JT[i]);
  });

  // ---- 智能消歧数据 ----
  var W_CHAR = 1 / 4, W_PAIR = 1 / 2;
  var charScore = new Map(), pairScore = new Map();
  (function () {
    var mode = 0;
    var lines = smartText.split('\n');
    for (var i = 0; i < lines.length; i++) {
      var line = lines[i];
      if (!line) continue;
      if (line === '#FREQ') { mode = 1; continue; }
      if (line === '#PAIR') { mode = 2; continue; }
      var t = line.indexOf('\t');
      if (t <= 0) continue;
      var v = parseFloat(line.slice(t + 1));
      if (!isFinite(v)) continue;
      if (mode === 1) charScore.set(line.charAt(0), Math.log(1 + v) * W_CHAR);
      else if (mode === 2) pairScore.set(line.slice(0, 2), Math.log(1 + v) * W_PAIR);
    }
  })();

  var candCache = new Map();
  function candidates(ch) {
    var cached = candCache.get(ch);
    if (cached) return cached;
    var list = [];
    var fi = ftIdx.get(ch);
    if (fi !== undefined) list.push(JT[fi]);
    var hc = hxCand.get(ch);
    if (hc) for (var k = 0; k < hc.length; k++) if (list.indexOf(hc[k]) < 0) list.push(hc[k]);
    if (jtIdx.has(ch) && list.indexOf(ch) < 0) list.push(ch);
    candCache.set(ch, list);
    return list;
  }

  function toMartian(s) {
    var out = '';
    for (var i = 0; i < s.length; i++) {
      var c = s.charAt(i), k;
      k = jtIdx.get(c);
      if (k === undefined) k = ftIdx.get(c);
      out += (k === undefined) ? c : HX[k];
    }
    return out;
  }

  function toSimplified(s) {
    var out = '';
    for (var i = 0; i < s.length; i++) {
      var c = s.charAt(i), k = ftIdx.get(c);
      if (k !== undefined) { out += JT[k]; continue; }
      var hc = hxCand.get(c);
      out += hc ? hc[0] : c;
    }
    return out;
  }

  function toTraditional(s) {
    var out = '';
    for (var i = 0; i < s.length; i++) {
      var c = s.charAt(i), k = jtIdx.get(c);
      if (k === undefined) k = hxFirst.get(c);
      out += (k === undefined) ? c : FT[k];
    }
    return out;
  }

  function toSimplifiedSmart(s) {
    var n = s.length;
    if (n === 0) return '';
    var cand = new Array(n), i, j, k;
    for (i = 0; i < n; i++) {
      var l = candidates(s.charAt(i));
      cand[i] = l.length ? l : [s.charAt(i)];
    }
    var prev = new Array(cand[0].length);
    for (j = 0; j < prev.length; j++) prev[j] = charScore.get(cand[0][j]) || 0;
    var back = new Array(n);
    back[0] = new Array(cand[0].length);
    for (j = 0; j < back[0].length; j++) back[0][j] = -1;

    for (i = 1; i < n; i++) {
      var cur = cand[i], pre = cand[i - 1];
      var now = new Array(cur.length), bk = new Array(cur.length);
      for (j = 0; j < cur.length; j++) {
        var cj = cur[j], best = -Infinity, bi = 0;
        for (k = 0; k < pre.length; k++) {
          var v = prev[k] + (pairScore.get(pre[k] + cj) || 0);
          if (v > best) { best = v; bi = k; }
        }
        now[j] = best + (charScore.get(cj) || 0);
        bk[j] = bi;
      }
      back[i] = bk;
      prev = now;
    }
    var bi2 = 0;
    for (j = 1; j < prev.length; j++) if (prev[j] > prev[bi2]) bi2 = j;
    var out = new Array(n), idx = bi2;
    for (i = n - 1; i >= 0; i--) {
      out[i] = cand[i][idx];
      idx = back[i][idx];
      if (idx < 0) idx = 0;
    }
    return out.join('');
  }

  function isChinese(c) { return jtIdx.has(c) || ftIdx.has(c); }
  function candidateCount(c) { return candidates(c).length; }

  return {
    toMartian: toMartian,
    toSimplified: toSimplified,
    toSimplifiedSmart: toSimplifiedSmart,
    toTraditional: toTraditional,
    isChinese: isChinese,
    candidateCount: candidateCount,
    tableSize: JT.length
  };
}

if (typeof module !== 'undefined' && module.exports) module.exports = { makeEngine: makeEngine };
