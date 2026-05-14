Authorization is the process of determining whether a user has the right to access a resource or perform an action. It is a critical aspect of security in any application.

## Authorization Type

**Role-Based Access Control (RBAC)**: This method assigns permissions to users based on their roles within an organization. Each role has a set of permissions that define what actions the user can perform.

## Implementation

### Roles and Permissions

| Role          | Business Role                  | Access          | Permissions                                                 |
|---------------|--------------------------------|-----------------|-------------------------------------------------------------|
| Administrator | **Administrator**              | All             | Subscribe a new customer, has full access to all resources except Billing. | 
| Manager       | **Customer**                   | Customer        | Can create and manage customer profile.                     | 
| Manager       | **Profiles Adjuster**          | Profile         | Can modify multiple users' profiles.                        |
| Manager       | **Booking Adjuster**           | Booking         | Can modify existing bookings.                               |
| Manager       | **Report Viewer**              | Reporting       | Can view reports.                                           |
| Manager       | **Configuration Manager**      | Configuration   | Can manage application configurations.                      |
| Accountant    | **Accounting**                 | Billing         | Can see invoices and manage payments.                       |
| User          | **Booking Requestor**          | Booking         | Can create and manage booking requests.                     |
| User          | **Profile Adjuster**           | User            | Can modify own user profile, setup preferences.             |
| User          | **Feedback Sender**            | Feedback        | Can send feedbacks.                                         |
| System        | **Report Builder**             | Reporting       | Can build reports.                                          |
| System        | **Billing Processor**          | Billing         | Can generate and process invoices and payments.             |
| IT Support    | **Auditor**                    | Audit           | Can see audit logs.                                         |

