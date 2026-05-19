describe('Campaign Management', () => {
  beforeEach(() => {
    cy.login('admin@vortexflow.local', 'Admin123!')
    cy.visit('/campaigns')
  })

  it('should display campaigns list', () => {
    cy.get('[class*="table"], [class*="list"]').should('be.visible')
  })

  it('should open campaign creation modal', () => {
    cy.get('[data-testid="create-campaign-btn"], [class*="create"]').click()
    cy.get('[class*="modal"], [class*="dialog"]').should('be.visible')
  })

  it('should create a new campaign', () => {
    const campaignName = `Test Campaign ${Date.now()}`

    cy.get('[data-testid="create-campaign-btn"], [class*="create"]').click()
    cy.get('[class*="modal"]').should('be.visible')

    cy.get('input[name="name"], input[id="campaign-name"]').type(campaignName)
    cy.get('textarea[name="description"], textarea[id="campaign-description"]').type('E2E Test Campaign Description')
    cy.get('select[name="platform"], [id="platform-select"]').select('Twitter')

    cy.get('[data-testid="save-campaign-btn"], button:contains("Guardar"), button:contains("Create")').click()

    cy.contains(campaignName).should('be.visible')
  })

  it('should edit existing campaign', () => {
    cy.get('[class*="campaign-item"]').first().click()
    cy.get('[data-testid="edit-campaign-btn"]').click()

    cy.get('input[name="name"]').clear().type('Updated Campaign Name')
    cy.get('[data-testid="save-campaign-btn"]').click()

    cy.contains('Updated Campaign Name').should('be.visible')
  })

  it('should delete a campaign', () => {
    cy.get('[class*="campaign-item"]').last().within(() => {
      cy.get('[data-testid="delete-campaign-btn"]').click()
    })

    cy.get('[class*="confirm-dialog"], button:contains("Confirmar"), button:contains("Delete")').click()

    cy.contains('deleted', 'eliminado').should('be.visible')
  })

  it('should filter campaigns by status', () => {
    cy.get('[data-testid="status-filter"], select[name="status"]').select('active')
    cy.wait(1000)
    cy.get('[class*="campaign-item"]').should('be.visible')

    cy.get('[data-testid="status-filter"], select[name="status"]').select('draft')
    cy.wait(1000)
  })

  it('should search campaigns', () => {
    cy.get('[data-testid="search-input"], input[type="search"]').type('Test')
    cy.wait(1000)
    cy.get('[class*="campaign-item"]').should('be.visible')
  })
})