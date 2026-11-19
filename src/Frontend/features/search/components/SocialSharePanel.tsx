"use client";

import { useState } from "react";
import { siteUrl } from "@/features/search/seo/metadata";

export function SocialSharePanel({
  path = "/",
  title = "Culinary Blog",
}: {
  path?: string;
  title?: string;
}) {
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

  const absoluteShareUrl = new URL(path, siteUrl).toString();
  const shareMessage = `Khám phá ${title} tại Culinary Blog`;
  const facebookUrl = new URL("https://www.facebook.com/sharer/sharer.php");
  facebookUrl.searchParams.set("u", absoluteShareUrl);
  facebookUrl.searchParams.set("quote", shareMessage);
  const xUrl = new URL("https://twitter.com/intent/tweet");
  xUrl.searchParams.set("url", absoluteShareUrl);
  xUrl.searchParams.set("text", shareMessage);

  return (
    <section aria-labelledby="share-title" className="border-t border-zinc-200 pt-8 dark:border-zinc-800">
      <div className="max-w-2xl">
        <p className="text-xs font-bold uppercase text-rose-400">Lan tỏa món ngon</p>
        <h2 id="share-title" className="mt-1 font-sans text-3xl font-semibold">Chia sẻ {title}</h2>
        <p className="mt-3 text-sm leading-6 text-zinc-500 dark:text-zinc-400">
          Gửi những công thức yêu thích đến bạn bè và cùng nhau vào bếp.
        </p>
        <div className="mt-5 flex flex-wrap gap-3">
          <a
            className="inline-flex min-h-11 items-center border border-zinc-300 px-4 text-sm font-semibold hover:border-emerald-400 hover:text-emerald-400 dark:border-zinc-700"
            href={facebookUrl.toString()}
            target="_blank"
            rel="noreferrer"
          >
            Facebook
          </a>
          <a
            className="inline-flex min-h-11 items-center border border-zinc-300 px-4 text-sm font-semibold hover:border-emerald-400 hover:text-emerald-400 dark:border-zinc-700"
            href={xUrl.toString()}
            target="_blank"
            rel="noreferrer"
          >
            X
          </a>
          <button
            className="inline-flex min-h-11 items-center border border-zinc-300 px-4 text-sm font-semibold hover:border-emerald-400 hover:text-emerald-400 dark:border-zinc-700"
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
