import { useEffect, useRef, useState } from 'react'
import type { WaveScene } from './WaveScene'

interface Props {
  className?: string
  /** 0..1, how much the camera follows the pointer */
  parallax?: number
  view?: 'shoulder' | 'tube'
}

/** Mounts the WebGL wave, pauses it off-screen, and falls back to a still gradient without WebGL. */
export function WaveCanvas({ className, parallax = 1, view = 'shoulder' }: Props) {
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const [failed, setFailed] = useState(false)
  const [ready, setReady] = useState(false)

  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return
    let disposed = false
    let cleanup = () => {}
    // three.js is loaded on demand, so the page text shows up before the 3D does
    import('./WaveScene').then(({ WaveScene: Scene }) => {
      if (disposed) return
      let scene: WaveScene
      try {
        scene = new Scene(canvas, { parallax, view })
      } catch {
        setFailed(true)
        return
      }
      const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches
      const resize = () => scene.setSize(canvas.clientWidth, canvas.clientHeight)
      resize()
      const ro = new ResizeObserver(resize)
      ro.observe(canvas)

      let visible = true
      const sync = () => {
        if (reduce) return scene.still()
        if (visible && !document.hidden) scene.start()
        else scene.stop()
      }
      const io = new IntersectionObserver(
        ([e]) => {
          visible = e.isIntersecting
          scene.setThrottle(e.intersectionRatio < 0.5)
          sync()
        },
        { threshold: [0, 0.25, 0.5, 0.75, 1] },
      )
      io.observe(canvas)
      document.addEventListener('visibilitychange', sync)
      sync()
      setReady(true)

      const onMove = (e: PointerEvent) => {
        scene.setPointer((e.clientX / window.innerWidth) * 2 - 1, -((e.clientY / window.innerHeight) * 2 - 1))
      }
      window.addEventListener('pointermove', onMove, { passive: true })

      cleanup = () => {
        ro.disconnect()
        io.disconnect()
        document.removeEventListener('visibilitychange', sync)
        window.removeEventListener('pointermove', onMove)
        scene.dispose()
      }
    })
    return () => {
      disposed = true
      cleanup()
    }
  }, [parallax, view])

  return (
    <div className={className}>
      {failed ? (
        <div className="h-full w-full bg-[radial-gradient(120%_80%_at_70%_20%,#0d6b94_0%,#051f38_60%)]" />
      ) : (
        <canvas
          ref={canvasRef}
          className={`block h-full w-full bg-[radial-gradient(120%_80%_at_70%_20%,#0d6b94_0%,#051f38_60%)] transition-opacity duration-[1400ms] ${ready ? 'opacity-100' : 'opacity-0'}`}
          aria-hidden="true"
        />
      )}
    </div>
  )
}
