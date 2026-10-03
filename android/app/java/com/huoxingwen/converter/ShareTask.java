package com.huoxingwen.converter;

import android.app.Activity;
import android.content.Intent;

/** 在界面线程里弹出系统分享面板。 */
public class ShareTask implements Runnable {

    private final Activity activity;
    private final String text;

    public ShareTask(Activity activity, String text) {
        this.activity = activity;
        this.text = text;
    }

    @Override
    public void run() {
        try {
            Intent i = new Intent(Intent.ACTION_SEND);
            i.setType("text/plain");
            i.putExtra(Intent.EXTRA_TEXT, text);
            activity.startActivity(Intent.createChooser(i, "分享到"));
        } catch (Exception ignored) {
        }
    }
}
