describe('Login Flow', () => {
  beforeEach(() => {
    cy.visit('/login')
  })

  it('should display login page correctly', () => {
    cy.get('input[type="email"], input[name="email"]').should('be.visible')
    cy.get('input[type="password"], input[name="password"]').should('be.visible')
    cy.get('button[type="submit"]').should('be.visible')
  })

  it('should login successfully with valid credentials', () => {
    cy.login('admin@vortexflow.local', 'Admin123!')
    cy.url().should('not.include', '/login')
    cy.get('[class*="dashboard"], [data-testid="dashboard"]').should('be.visible')
  })

  it('should show error with invalid credentials', () => {
    cy.get('input[type="email"], input[name="email"]').first().type('invalid@test.com')
    cy.get('input[type="password"], input[name="password"]').first().type('wrongpassword')
    cy.get('button[type="submit"]').click()
    cy.contains('error', 'incorrecto', 'inválido', 'fallo').should('be.visible')
  })

  it('should validate required email field', () => {
    cy.get('button[type="submit"]').click()
    cy.contains('required', 'obligatorio', 'requerido').should('be.visible')
  })

  it('should logout successfully', () => {
    cy.login('admin@vortexflow.local', 'Admin123!')
    cy.url().should('not.include', '/login')
    cy.getByTestId('user-menu').click()
    cy.getByTestId('logout-button').click()
    cy.url().should('include', '/login')
  })
})