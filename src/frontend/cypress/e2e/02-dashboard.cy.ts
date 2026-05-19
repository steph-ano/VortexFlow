describe('Dashboard', () => {
  beforeEach(() => {
    cy.login('admin@vortexflow.local', 'Admin123!')
    cy.visit('/dashboard')
  })

  it('should load dashboard without errors', () => {
    cy.get('[class*="dashboard"], [data-testid="dashboard"]').should('be.visible')
  })

  it('should display trend charts', () => {
    cy.waitForElement('[class*="chart"], canvas, [class*="echart"]', 15000)
    cy.get('[class*="chart"], canvas, [class*="echart"]').should('have.length.greaterThan', 0)
  })

  it('should display platform statistics cards', () => {
    cy.get('[class*="card"], [class*="stat"]').should('be.visible')
    cy.contains('Twitter', 'Instagram', 'LinkedIn', 'Facebook').should('be.visible')
  })

  it('should show trend data without console errors', () => {
    cy.window().then((win) => {
      const errors: string[] = []
      win.console.error = (...args) => {
        errors.push(args.join(' '))
      }
      cy.wait(2000)
      cy.wrap(errors).should('be.empty')
    })
  })

  it('should display real-time updates indicator', () => {
    cy.get('[class*="realtime"], [class*="live"], [data-testid="connection-status"]')
      .should('contain', 'tiempo real', 'live', 'connected')
  })

  it('should navigate to different time ranges', () => {
    cy.get('[class*="time-range"], [data-testid="time-range-selector"]').should('be.visible')
    cy.get('[class*="time-range"], [data-testid="time-range-selector"]').contains('24h').click()
    cy.wait(1000)
    cy.get('[class*="time-range"], [data-testid="time-range-selector"]').contains('7d').click()
    cy.wait(1000)
  })

  it('should refresh data manually', () => {
    cy.get('[data-testid="refresh-button"], [class*="refresh"]').click()
    cy.wait(1000)
    cy.get('[class*="loading"], [class*="spinner"]').should('not.exist')
  })
})