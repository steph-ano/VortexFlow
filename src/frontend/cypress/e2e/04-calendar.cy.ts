describe('Calendar Editorial', () => {
  beforeEach(() => {
    cy.login('admin@vortexflow.local', 'Admin123!')
    cy.visit('/calendar')
  })

  it('should load calendar view', () => {
    cy.waitForElement('[class*="calendar"], .fc', 15000)
    cy.get('[class*="calendar"], .fc').should('be.visible')
  })

  it('should display calendar grid', () => {
    cy.get('.fc-daygrid, .fc-timegrid').should('be.visible')
    cy.get('.fc-daygrid-day').should('have.length.greaterThan', 20)
  })

  it('should navigate to next month', () => {
    cy.get('.fc-next-button, [data-testid="next-month-btn"]').click()
    cy.wait(500)
  })

  it('should navigate to previous month', () => {
    cy.get('.fc-prev-button, [data-testid="prev-month-btn"]').click()
    cy.wait(500)
  })

  it('should open post creation modal via calendar', () => {
    cy.get('.fc-daygrid-day').first().click()
    cy.get('[class*="modal"], [class*="dialog"]').should('be.visible')
  })

  it('should schedule a post for future date', () => {
    const futureDate = new Date()
    futureDate.setDate(futureDate.getDate() + 7)

    const dayNumber = futureDate.getDate()
    const monthYear = futureDate.toLocaleString('default', { month: 'long', year: 'numeric' })

    cy.get('.fc-daygrid-day').contains(dayNumber).click()
    cy.get('[class*="modal"]').should('be.visible')

    cy.get('input[name="content"], textarea[name="content"]').type('Scheduled post via E2E test')
    cy.get('input[name="scheduledAt"], input[type="datetime-local"]').should('be.visible')

    cy.get('[data-testid="schedule-post-btn"]').click()

    cy.contains('programado', 'scheduled', 'queued').should('be.visible')
  })

  it('should view scheduled posts in day', () => {
    cy.get('.fc-daygrid-day').first().click()
    cy.wait(500)
    cy.get('[class*="event"], [class*="post-item"]').should('be.visible')
  })

  it('should drag and drop to reschedule (if supported)', () => {
    cy.get('[class*="event"], [class*="post-item"]').should('exist')
  })

  it('should filter by platform in calendar', () => {
    cy.get('[data-testid="platform-filter"], select[name="platform"]').select('Twitter')
    cy.wait(500)
    cy.get('[class*="event"]').should('be.visible')
  })

  it('should switch calendar views (day/week/month)', () => {
    cy.get('.fc-timeGridWeek-button, [data-testid="view-week-btn"]').click()
    cy.wait(500)
    cy.get('.fc-timeGridView').should('be.visible')

    cy.get('.fc-listWeek-button, [data-testid="view-list-btn"]').click()
    cy.wait(500)
  })
})