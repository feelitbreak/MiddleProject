**General Task Description**  
Your task is to build a project with a **microservices architecture** that interacts with an **unstable external API** deployed in a Docker environment.  
The application must include:

* Reading and processing data from the unstable API,

* Distributing data through a message queue,

* Storing and aggregating data in a database,

* A frontend interface with dashboards and visualizations,

* Both **GraphQL API** and **REST API**,

* Implementation of **CI/CD**, testing, and DevOps infrastructure.

Unstable external Api: [https://github.com/nantonov/WeakApp](https://github.com/nantonov/WeakApp)      
**Architecture and Microservices**  
At minimum, the system should include:  
Data Injector Service

* Fetches data from the unstable API and pushes it into the queue.

Data Processor Service

* Reads messages from the queue and persists them into the database.

GraphQL API Gateway

* Provides the frontend with access to data from the database through GraphQL.

* Requirements:

* Support filtering, pagination, and aggregations (e.g., by time or location),

* Strongly-typed schema.

Notification Service

* Uses **SignalR/WebSockets** to send real-time updates to clients.

Frontend

* Implemented in **React** or **Angular**,

* Works with the GraphQL API (Apollo / URQL / Relay),

* Displays dashboards with:

* Latest values,

* Charts,

* Aggregations by location/type,

* Receives notifications via SignalR,

* Responsive and clean UI,

* Proper error and loading state handling,

* (Optional) Filtering/search/aggregations by time.

**DevOps and Deployment**  
The entire application must run with a single **docker-compose.yml**, including:

* The unstable API (provided),

* Message queue (RabbitMQ or Kafka),

* Database (any),

* All microservices,

* Frontend.

**CI/CD and GitHub**  
Each microservice and the frontend must have:

* A dedicated GitHub Actions pipeline,

* Unit tests, build, and linting,

* Docker image build.

* (Optional) SonarQube

* (Optional) End-to-end tests (e.g., Playwright).

Git Practices

* Use a branch strategy (e.g., main \+ feature/\* or GitFlow),

* All changes go through Pull Requests,

* Commit history should be:

* Clean and readable,

* Squash-merge or rebase is acceptable.

**Technical Requirements**

* Mandatory use of **GraphQL**,

* Mandatory use of a **message queue** (RabbitMQ or Kafka),

* Latest **.NET** version,

* Data persistence in a database,

* Error handling \+ logging (e.g., Serilog),

* Unit \+ Integration tests,

* SignalR (or WebSockets) to demonstrate real-time features.

* Clean code 

**Bonus Points**

* Microservices written in different languages (e.g., .NET, Python, Node.js) to demonstrate polyglot architecture.

* Metrics (e.g., OpenTelemetry \+ Prometheus \+ Grafana),

* Auto-deployment (even a mock script is fine),

* API documentation (GraphQL Playground / Swagger for internal services),

* Static analysis, code style checks, StyleCop,

* Use of advanced libraries (Polly, FluentValidation, MediatR, etc.).

* Secure communication between microservices

* Use of different software architecture approaches (DDD, TDD, Clean Architecture, N-Layered architecture, CQRS, etc.)

* No [code smells](https://en.wikipedia.org/wiki/Code_smell)

* No warnings from IDE / SonarQube
