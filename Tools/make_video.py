"""Assemble PNG frames into an MP4 (H.264). Usage: python Tools/make_video.py <frames_dir> <out.mp4> [fps]"""
import sys, os, glob, imageio.v2 as imageio
frames_dir, out = sys.argv[1], sys.argv[2]
fps = int(sys.argv[3]) if len(sys.argv) > 3 else 30
files = sorted(glob.glob(os.path.join(frames_dir, "frame_*.png")))
if not files:
    print("no frames"); sys.exit(1)
w = imageio.get_writer(out, fps=fps, codec="libx264", quality=8, macro_block_size=8, ffmpeg_params=["-pix_fmt", "yuv420p"])
for i, f in enumerate(files):
    w.append_data(imageio.imread(f))
w.close()
print(f"wrote {out}: {len(files)} frames @ {fps} fps, {os.path.getsize(out)//1024} KB")
