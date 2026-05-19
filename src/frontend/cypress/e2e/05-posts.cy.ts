describe('Posts Management', () => {
  beforeEach(() => {
    cy.login('admin@vortexflow.local', 'Admin123!')
    cy.visit('/posts')
  })

  it('should display posts list', () => {
    cy.get('[class*="table"], [data-testid="posts-table"]').should('be.visible')
  })

  it('should filter posts by status', () => {
    cy.get('[data-testid="status-filter"], select[name="status"]').select('published')
    cy.wait(1000)

    cy.get('[data-testid="status-filter"], select[name="status"]').select('failed')
    cy.wait(1000)

    cy.get('[data-testid="status-filter"], select[name="status"]').select('pending')
    cy.wait(1000)
  })

  it('should view post details', () => {
    cy.get('[class*="post-item"]').first().click()
    cy.get('[class*="modal"], [class*="detail"]').should('be.visible')
  })

  it('should retry a failed post', () => {
    cy.get('[data-testid="status-filter"], select[name="status"]').select('failed')
    cy.wait(500)

    cy.get('[class*="post-item"]').should('exist').then(($posts) => {
      if ($posts.length > 0) {
        cy.wrap($posts).first().within(() => {
          cy.get('[data-testid="retry-btn"], [class*="retry"]').click()
        })

        cy.contains('reintentando', 'retrying', 'scheduled').should('be.visible')
      } else {
        cy.log('No failed posts to retry')
      }
    })
  })

  it('should view post content preview', () => {
    cy.get('[class*="post-item"]').first().within(() => {
      cy.get('[class*="content-preview"]').should('be.visible')
    })
  })

  it('should display post metrics', () => {
    cy.get('[class*="post-item"]').first().within(() => {
      cy.get('[class*="metrics"], [class*="stats"]').should('be.visible')
    })
  })

  it('should search posts', () => {
    cy.get('[data-testid="search-input"], input[type="search"]').type('test')
    cy.wait(1000)
    cy.get('[class*="post-item"]').should('be.visible')
  })

  it('should paginate posts', () => {
    cy.get('[data-testid="pagination"], .pagination').should('exist').then(($pagination) => {
      if ($pagination.find('[data-testid="next-page"]').length > 0) {
        cy.get('[data-testid="next-page"]').click()
        cy.wait(500)
      }
    })
  })
})