/// <reference types="cypress" />

declare namespace Cypress {
  interface Chainable {
    login(email: string, password: string): Chainable<void>
    logout(): Chainable<void>
    getByTestId(testId: string): Chainable<JQuery<HTMLElement>>
    assertRoute(path: string): Chainable<void>
    waitForElement(selector: string, timeout?: number): Chainable<void>
  }
}

Cypress.Commands.add('login', (email: string, password: string) => {
  cy.visit('/login')
  cy.get('input[type="email"], input[name="email"], input[id="email"]').first().clear().type(email)
  cy.get('input[type="password"], input[name="password"], input[id="password"]').first().clear().type(password)
  cy.get('button[type="submit"], button:contains("Iniciar"), button:contains("Login"), button:contains("Entrar")').first().click()
  cy.url().should('not.include', '/login')
})

Cypress.Commands.add('logout', () => {
  cy.getByTestId('user-menu').click()
  cy.getByTestId('logout-button').click()
  cy.url().should('include', '/login')
})

Cypress.Commands.add('getByTestId', (testId: string) => {
  return cy.get(`[data-testid="${testId}"]`)
})

Cypress.Commands.add('assertRoute', (path: string) => {
  cy.url().should('include', path)
})

Cypress.Commands.add('waitForElement', (selector: string, timeout = 10000) => {
  cy.get(selector, { timeout }).should('be.visible')
})