"use client";

import { useState } from "react";
import { siteUrl } from "@/features/search/seo/metadata";

export function SocialSharePanel() {
  const [copied, setCopied] = useState(false);

  async function copyLink() {
    try {
      await navigator.clipboard.writeText(window.location.href);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 2000);
    } catch {
      setCopied(false);
    }
  }

  const shareUrl = encodeURIComponent(siteUrl);

  return (
    <section aria-labelledby="share-title" className="border-t border-white/15 pt-8">
      <div className="max-w-2xl">
        <p className="text-xs font-bold uppercase text-[var(--color-tomato)]">Lan tỏa món ngon</p>
        <h2 id="share-title" className="mt-1 font-sans text-3xl font-semibold">Chia sẻ Culinary Blog</h2>
        <p className="mt-3 text-sm leading-6 text-[var(--muted)]">
          Gửi những công thức yêu thích đến bạn bè và cùng nhau vào bếp.
        </p>
        <div className="mt-5 flex flex-wrap gap-3">
          <a
            className="inline-flex min-h-11 items-center border border-white/20 px-4 text-sm font-semibold hover:border-[var(--color-leaf)] hover:text-[var(--color-leaf)]"
            href={`https://www.facebook.com/sharer/sharer.php?u=${shareUrl}`}
            target="_blank"
            rel="noreferrer"
          >
            Facebook
          </a>
          <a
            className="inline-flex min-h-11 items-center border border-white/20 px-4 text-sm font-semibold hover:border-[var(--color-leaf)] hover:text-[var(--color-leaf)]"
            href={`https://twitter.com/intent/tweet?url=${shareUrl}&text=${encodeURIComponent("Khám phá công thức nấu ăn tại Culinary Blog")}`}
            target="_blank"
            rel="noreferrer"
          >
            X
          </a>
          <button
            className="inline-flex min-h-11 items-center border border-white/20 px-4 text-sm font-semibold hover:border-[var(--color-leaf)] hover:text-[var(--color-leaf)]"
            type="button"
            onClick={copyLink}
          >
            {copied ? "Đã sao chép liên kết" : "Sao chép liên kết"}
          </button>
        </div>
      </div>
    </section>
  );
}
