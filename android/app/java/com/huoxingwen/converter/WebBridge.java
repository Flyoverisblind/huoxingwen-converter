package com.huoxingwen.converter;

import android.app.Activity;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Context;
import android.webkit.JavascriptInterface;

/**
 * 注入到网页里的原生能力桥接：读取剪贴板、调用系统分享。
 * （独立顶层类，方法名会暴露给页面 JavaScript 调用。）
 */
public class WebBridge {

    private final Activity activity;

    public WebBridge(Activity activity) {
        this.activity = activity;
    }

    /** 页面里的“粘贴”按钮：返回剪贴板文本，没有则返回空串。 */
    @JavascriptInterface
    public String paste() {
        try {
            ClipboardManager cm = (ClipboardManager) activity.getSystemService(Context.CLIPBOARD_SERVICE);
            if (cm == null || !cm.hasPrimaryClip()) return "";
            ClipData clip = cm.getPrimaryClip();
            if (clip == null || clip.getItemCount() == 0) return "";
            CharSequence cs = clip.getItemAt(0).coerceToText(activity);
            return cs == null ? "" : cs.toString();
        } catch (Exception e) {
            return "";
        }
    }

    /** 页面里的“分享”按钮：把文本交给系统分享面板。 */
    @JavascriptInterface
    public void share(String text) {
        if (text == null || text.length() == 0) return;
        activity.runOnUiThread(new ShareTask(activity, text));
    }
}
