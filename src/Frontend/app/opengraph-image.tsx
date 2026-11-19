import { ImageResponse } from "next/og";

export const size = { width: 1200, height: 630 };
export const contentType = "image/png";

export default function OpenGraphImage() {
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
        <div style={{ fontSize: 72, fontWeight: 700 }}>Công thức nấu ăn</div>
        <div style={{ fontSize: 32, marginTop: 24 }}>Khám phá và chia sẻ món ngon mỗi ngày</div>
      </div>
    ),
    size,
  );
}
