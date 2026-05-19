import './commands'

declare global {
  interface Window {
    VortexFlow: {
      currentUser: {
        id: string
        email: string
        role: string
      }
    }
  }
}