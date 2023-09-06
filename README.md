# ReservationSystem
ReservationSystem is an application built on .NET Core 7 and MongoDB

Data:

+User and authority data provided by MongoDB

+ReservationPoint  - parking site
  string _id
  string name
  array reservations -auxiliary collection of Reservation._id

+Reservation      -reservation made by user for a particular day
  string   _id
  string   userid - foreign key to user
  string   rpid   - foreign key to ReservationPoint
  datetime date   - date of validity
  
