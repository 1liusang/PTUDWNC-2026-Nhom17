import { ImageResponse } from "next/og";
import { getRecipeBySlug } from "@/features/recipes/api/client";

export const size = { width: 1200, height: 630 };
export const contentType = "image/png";

export default async function RecipeOpenGraphImage({
  params,
}: {
  params: Promise<{ slug: string }>;
}) {
  const { slug } = await params;
  let title = "Công thức nấu ăn";
  try {
    const recipe = await getRecipeBySlug(slug);
    title = recipe.title;
  } catch {
    // Keep a valid share image available if the recipe API is temporarily unavailable.
  }

  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          flexDirection: "column",
          justifyContent: "center",
          padding: 80,
          background: "linear-gradient(135deg, #132a22 0%, #315d45 100%)",
          color: "#fff8e8",
          fontFamily: "sans-serif",
        }}
      >
        <div style={{ fontSize: 30, color: "#d7b56d", marginBottom: 24 }}>CULINARY BLOG</div>
        <div style={{ fontSize: 64, fontWeight: 700, lineHeight: 1.2 }}>{title}</div>
        <div style={{ fontSize: 32, marginTop: 24 }}>Công thức nấu ăn</div>
      </div>
    ),
    size,
  );
}
