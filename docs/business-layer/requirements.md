---
title: Business Requirements
---

## Scenario

The company has a limited number of parking slots, typically fewer than the number of employees. These slots are offered to employees who can request a parking slot for any available time slot (day or half day). Currently, employees send emails to HR, where parking requests are managed using an external system or a simple Excel file. This approach has several issues, including fairness and the workload involved in processing all email requests. The typical "first come, first served" method is simple but not fair and adds significant administrative burden. The main idea behind the new system is to ensure fairness for everyone, with minimal or no manual adjustment/management required.

## Business Requirements

### 1. Automated Parking Management System
The company requires an automated parking management system to streamline the process of booking and managing parking slots. This system should eliminate the need for manual email requests and reduce administrative workload.

### 2. Fair Distribution of Parking Slots
The system must ensure a fair distribution of parking slots, prioritizing occasional parkers over daily users. It should prevent informal agreements and unauthorized use of parking slots.

### 3. Dedicated Motorcycle Parking
The system should include dedicated and secure parking areas for motorcycles to optimize space usage and prevent damage to bikes.

### 4. Real-Time Updates and Notifications
Employees need real-time updates on parking availability and notifications about their parking slot status. This feature will help reduce frustration and improve overall user experience.

### 5. Integration with Calendar and Reminders
The system should integrate with employees' calendars and provide reminders for parking reservations. This will help employees manage their parking needs efficiently.

### 6. Comprehensive Reporting and Analytics
HR and management require detailed reports and analytics on parking slot usage. The system should provide insights into patterns, peak usage times, and underutilized slots to optimize resource allocation.

### 7. Sustainability and Eco-Friendly Features
The system should support the company's sustainability goals by incentivizing eco-friendly commuting options. Features such as priority parking for carpoolers and electric vehicle charging stations should be included.

### 8. User-Friendly Interface
The system must have an intuitive and user-friendly interface to ensure ease of use for all employees, including those responsible for managing parking assignments.

### 9. Security and Access Control
The system should include security measures to prevent unauthorized access and ensure that only eligible employees can book and use parking slots.

### 10. Scalability and Flexibility
The system should be scalable to accommodate future growth and flexible enough to adapt to changing business needs and policies.

### 11. Data-Driven Decision Making
The system should provide data and analytics to support data-driven decision-making, helping HR and management optimize parking resource allocation and improve overall efficiency.

### 12. Enhanced Communication Tools
The system should include communication tools to facilitate better interaction between employees and HR regarding parking slot management and availability.

## Table of Contents

1. [Parking Slot Allocation Process](#parking-slot-allocation-process)
    1. [Request Submission](#request-submission)
    2. [Request Validation](#request-validation)
    3. [Notification System](#notification-system)
    4. [Penalty Management](#penalty-management)
    5. [Reporting and Analytics](#reporting-and-analytics)
    6. [Priority Management](#priority-management)
    7. [Conflict Resolution](#conflict-resolution)
    8. [Feedback Mechanism](#feedback-mechanism)
    9. [Integration with Other Systems](#integration-with-other-systems)
    10. [Scalability](#scalability)
    11. [User Training and Support](#user-training-and-support)
    12. [Environmental Considerations](#environmental-considerations)
    13. [Draw Process](#draw-process)
    14. [Request Scheduler](#request-scheduler)
    15. [Request Approvals/Cancellations](#request-approvalscancellations)
2. [Core Values](#core-values)
3. [Apps](#apps)
    1. [Web App](#web-app)
    2. [Mobile App](#mobile-app)
4. [Microservices](#microservices)
5. [Auditing](#auditing)
6. [Expected Profit](#expected-profit)
7. [Glossary](#glossary)

### Parking Slot Allocation Process

#### Request Submission

Employees submit their parking slot requests through the system, specifying their preferred time slots (day or half day). The system collects these requests and prepares them for the Draw process.

#### Request Validation

The system validates each request to ensure it meets the criteria (e.g., no duplicate requests for the same time slot). Invalid requests are flagged, and employees are notified to correct and resubmit them.

#### Notification System

Employees receive notifications about the status of their requests (submitted, approved, rejected, or canceled). Notifications are sent via email and can also be viewed in the web or mobile app.

#### Penalty Management

The system tracks penalties for late cancellations or missed parking slots. Penalties are applied according to predefined rules and affect the employee's future parking slot allocations.

#### Reporting and Analytics

The system generates reports on parking slot usage, request patterns, and penalty statistics. These reports help management understand parking demand and optimize the allocation process.

#### Priority Management

The system allows for prioritization of parking slot requests based on predefined criteria such as seniority, job role, or special needs. This ensures that employees with higher priority have a better chance of securing a parking slot.

#### Conflict Resolution

In case of conflicting requests (e.g., multiple employees requesting the same slot), the system uses a fair algorithm to resolve conflicts. This may include random selection, rotating priority, or other equitable methods.

#### Feedback Mechanism

Employees can provide feedback on the parking allocation process through the system. This feedback is collected and analyzed to continuously improve the system and address any concerns or suggestions from employees.

#### Integration with Other Systems

The parking allocation system can integrate with other company systems such as HR, payroll, and employee management. This ensures seamless data flow and reduces the need for manual data entry.

#### Scalability

The system is designed to scale with the company's growth. As the number of employees and parking slots increases, the system can handle the additional load without performance degradation.

#### User Training and Support

Comprehensive training materials and support are provided to ensure employees can effectively use the system. This includes user manuals, video tutorials, and a helpdesk for resolving any issues.

#### Environmental Considerations

The system encourages carpooling and the use of eco-friendly vehicles by offering incentives such as priority parking slots or reduced penalties. This supports the company's sustainability goals and reduces its carbon footprint.

#### Draw Process

The Draw process is an automated mechanism that runs at a predetermined schedule to allocate parking slots among employees. During this process, the system evaluates all collected requests and assigns available slots based on a fair and transparent algorithm. This algorithm takes into account various factors such as the number of requests, existing assignments, penalties for late cancellations or missed slots, privileges for company car users, and overall user behavior patterns.

Once the Draw is complete, employees are notified of the outcome via email and through the web or mobile app. Notifications include whether their request has been approved or rejected. The system is designed to be self-managing, requiring no manual confirmation from employees for their allocated slots. This ensures minimal administrative overhead and a seamless user experience.

The Draw process aims to balance fairness and efficiency, ensuring that all employees have an equitable chance of securing a parking slot while maintaining the core values of the system.

The complete description is on [Business Process](./process).

#### Request Scheduler

Employees can setup that system will send scheduled requests automatically for prefered days, so they don't have to do it manually. 
Some slots are long term booked for people with a company car. Requests from these people are always pre-assigned on park slots and penalization is not applied. 
Since each person/employee can schedule which days they need the park slot on their side, very little management/interaction is needed in fact.

#### Request Approvals/Cancellations

Employees can cancel their parking slot requests at any time. If a request is canceled before the Draw process, it has no impact on the allocation of parking slots. However, cancellations made after the Draw process, as well as missed parking slots, will incur penalties. This is to maintain the system's **"Simple"** core value by discouraging last-minute changes that complicate the allocation process.

When a parking slot becomes available due to a cancellation or a missed slot, the system will promptly offer it to other interested employees. The first employee to confirm the newly available slot will be assigned to it. This process is designed to be user-friendly and can be easily managed through the mobile application or the web interface.



### Apps

#### Web App

The web app is divided into three main sections: User, Management, and Admin.

* **User Section (My)**
    * **My Requests** - Users can view and manage their parking slot requests.
    * **My Parking** - Users can see their current and upcoming parking slot assignments.
    * **My Info** - Users can manage their personal information, including car details (license plates, ID, names).

* **Manager Section**
    * **Manage Users** - Managers can oversee user accounts and their parking requests.
    * **View All Requests** - Managers have access to all parking requests and can approve or reject them with reasons.
    * **User Capabilities** - Managers have all the capabilities of a regular user.

* **Admin Section**
    * **Assign Roles** - Admins can assign roles to users, such as Manager or User.
    * **Manager Capabilities** - Admins have all the capabilities of a manager and user.

* **System Section**
    * **Business Process Automation** - The system runs the business processes, ensuring smooth operation and minimal manual intervention.

#### Mobile App

The mobile app provides a user-friendly interface for employees to interact with the parking allocation system.

![Scatch](../images/mobileapp-sketch.png)

* **Send Requests** - Users can submit parking slot requests.
* **Confirm Assignments** - Users can confirm their assigned parking slots.
* **View Status** - Users can check the status of their requests.
* **Request History** - Users can view their past parking requests and assignments.

#### Microservices

The system is built using a microservices architecture, ensuring flexibility and scalability.

* **Flexible Business Processes** - The system can easily adjust business processes with minimal development effort.
* **Scalable Architecture** - The system can handle increased load as the company grows.

#### Auditing

The system includes comprehensive auditing features.

* **Log Exploration** - Users can easily explore logs and traces to monitor system activity.
* **Traceability** - The system provides detailed traceability for all actions, ensuring transparency and accountability.


### Expected Profit

The system is not meant to bring direct profit, but it provides valuable reports that can help with monetization. For example, charging a nominal fee (e.g., 1 Euro) for each allocated time slot can help companies offset licensing costs or fund employee amenities like coffee.

Other monetization options include:

1. **Subscription Model**: Implement a subscription-based model where employees pay a monthly or annual fee for access to premium parking slots or additional features within the system.

2. **Incentive Programs**: Offer incentives for employees who frequently use eco-friendly vehicles or participate in carpooling. These incentives could be sponsored by external partners or internal company funds.

3. **Advertising**: Integrate non-intrusive advertisements within the web and mobile apps. Local businesses or service providers could advertise their offerings to employees, generating additional revenue.

4. **Corporate Sponsorships**: Partner with local businesses or service providers to sponsor parking slots. For example, a nearby car wash could sponsor a slot and offer discounts to employees who park there.

5. **Auction System**: Extend the Draw process with an auction feature where employees can bid for premium parking slots. This could generate additional revenue and provide a fair way to allocate high-demand slots.

6. **Data Analytics Services**: Offer anonymized data analytics services to third parties interested in understanding parking trends and employee behavior. This data could be valuable for urban planning, transportation services, and other sectors.

By implementing these monetization strategies, the system can not only cover its operational costs but also provide additional benefits to the company and its employees.


### Glossary

| **Term**                | **Explanation**                                                                                       |
|-------------------------|-------------------------------------------------------------------------------------------------------|
| **Allocation**          | The process of assigning available parking slots to employees based on their requests and the system's fair algorithm. |
| **Cancellation**        | The act of withdrawing a parking slot request. Cancellations made after the Draw process may incur penalties. |
| **Draw Process**        | An automated mechanism that allocates parking slots among employees based on a fair and transparent algorithm. |
| **Eco-friendly Vehicles** | Vehicles that have a reduced environmental impact, often incentivized with priority parking slots. |
| **Fair Algorithm**      | A method used by the system to ensure equitable allocation of parking slots among employees. |
| **Notification**        | A message sent to employees informing them of the status of their parking slot requests. |
| **Penalty**             | A consequence applied for late cancellations or missed parking slots, affecting future allocations. |
| **Priority Management** | A system feature that allows prioritization of parking slot requests based on criteria such as seniority or job role. |
| **Request**             | An employee's submission for a parking slot for a specific time period. |
| **Scalability**         | The system's ability to handle increased load as the company grows without performance degradation. |
| **Sustainability**      | Efforts to promote environmental responsibility, such as encouraging carpooling and the use of eco-friendly vehicles. |
| **Validation**          | The process of checking parking slot requests to ensure they meet predefined criteria. |
| **Web App**             | A web-based application that allows employees to interact with the parking allocation system. |
| **Mobile App**          | A mobile application that provides a user-friendly interface for employees to manage their parking slot requests. |
| **Microservices**       | An architectural style that structures the system as a collection of loosely coupled services, ensuring flexibility and scalability. |
| **Auditing**            | The process of logging and tracing system activities to ensure transparency and accountability. |
| **Subscription Model**  | A monetization strategy where employees pay a fee for access to premium parking slots or additional features. |
| **Auction System**      | A feature that allows employees to bid for premium parking slots, generating additional revenue. |
| **Data Analytics Services** | Services that offer insights into parking trends and employee behavior, potentially valuable for third parties. |
| **Corporate Sponsorships** | Partnerships with local businesses to sponsor parking slots, providing additional benefits to employees. |