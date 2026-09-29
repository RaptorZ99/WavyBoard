import { motion, useReducedMotion } from 'motion/react'

interface Props {
  text: string
  className?: string
}

/** The title as water: a swell rolls through the letters by widening and thickening them in turn. */
export function SwellTitle({ text, className }: Props) {
  const reduce = useReducedMotion()
  return (
    <h1 className={className} aria-label={text}>
      {[...text].map((ch, i) => (
        <motion.span
          key={i}
          aria-hidden="true"
          className="inline-block"
          initial={reduce ? false : { y: '0.9em', opacity: 0 }}
          animate={{ y: 0, opacity: 1 }}
          transition={{ duration: 1.1, delay: 0.25 + i * 0.06, ease: [0.22, 1, 0.36, 1] }}
        >
          <span className="swell-letter" style={{ animationDelay: `${1.2 + i * 0.22}s` }}>
            {ch}
          </span>
        </motion.span>
      ))}
    </h1>
  )
}
