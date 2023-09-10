using IdentityMongo.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Diagnostics.Eventing.Reader;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.VisualBasic;
using System.ComponentModel;
using IdentityMongo.Controllers;
using System.Drawing.Drawing2D;
//using AspNetCore.Identity.MongoDbCore.Infrastructure;

namespace IdentityMongo.Services
{


    public class MongoDbReservationService
    {
        private readonly IMongoCollection<ReservationPoint> _ReservationPiontsCollection;
        private readonly IMongoCollection<Reservation> _ReservationsCollection;
        public TimeZoneInfo TimeZone { get; private set; }
        public MongoDbReservationService(IOptions<MongoDbSettings> mongoDbSettings)
        {
            MongoClient client = new MongoClient(mongoDbSettings.Value.ConnectionURI);
            IMongoDatabase database = client.GetDatabase(mongoDbSettings.Value.DatabaseName);
            _ReservationPiontsCollection = database.GetCollection<ReservationPoint>(mongoDbSettings.Value.CollectionNameP);
            _ReservationsCollection = database.GetCollection<Reservation>(mongoDbSettings.Value.CollectionNameR);
            TimeZone = TimeZoneInfo.GetSystemTimeZones().Where(x => x.DisplayName.Contains(@"Berlin")).FirstOrDefault();
        }
        public async Task CreateAsyncP(ReservationPoint reservationPoint)
        {
            await _ReservationPiontsCollection.InsertOneAsync(reservationPoint);
            return;
        }
        public async Task<bool> CreateAsyncR(string pointId, string userId, DateOnly date)
        {
            if (pointId == null || userId == null) return false;

            Reservation reservation = new Reservation(userId, pointId, date);
            await _ReservationsCollection.InsertOneAsync(reservation);

            FilterDefinition<ReservationPoint> filter = Builders<ReservationPoint>.Filter.Eq("Id", pointId);
            UpdateDefinition<ReservationPoint> update = Builders<ReservationPoint>.Update.AddToSet<string>("reservations", reservation.Id);
            await _ReservationPiontsCollection.UpdateOneAsync(filter, update);
            return true;
        }
        public async Task<bool> CreateAsyncR(ReservationPoint p, string userId, DateOnly date)
        {
            if (p.Id == null || userId == null) return false;

            Reservation reservation = new Reservation(userId, p.Id, date);
            await _ReservationsCollection.InsertOneAsync(reservation);

            FilterDefinition<ReservationPoint> filter = Builders<ReservationPoint>.Filter.Eq("Id", p.Id);
            UpdateDefinition<ReservationPoint> update = Builders<ReservationPoint>.Update.AddToSet<string>("reservations", reservation.Id);
            await _ReservationPiontsCollection.UpdateOneAsync(filter, update);
            return true;
        }
        public async Task<bool> DeleteAsyncR(ReservationPoint p, string reservationId)
        {
            FilterDefinition<ReservationPoint> filter = Builders<ReservationPoint>.Filter.Eq("Id", p.Id);
            UpdateDefinition<ReservationPoint> update = Builders<ReservationPoint>.Update.Pull(x => x.ReservationIDs, reservationId);
            await _ReservationPiontsCollection.UpdateOneAsync(filter, update);

            FilterDefinition<Reservation> filterr = Builders<Reservation>.Filter.Eq("Id", reservationId);
            _ReservationsCollection.DeleteOne(filterr); 
            
            return true;
            
        }
        public async Task<bool> DeleteAsyncR(string reservationId)
        {
            Reservation r = await GetAsyncR(reservationId);
            if (r == null) return false;

            FilterDefinition<ReservationPoint> filter = Builders<ReservationPoint>.Filter.Eq("Id", r.RpId);
            UpdateDefinition<ReservationPoint> update = Builders<ReservationPoint>.Update.Pull(x => x.ReservationIDs, reservationId);
            await _ReservationPiontsCollection.UpdateOneAsync(filter, update);

            FilterDefinition<Reservation> filterr = Builders<Reservation>.Filter.Eq("Id", reservationId);
            _ReservationsCollection.DeleteOne(filterr);

            return true;
        }
        public async Task<List<ReservationPoint>> GetAsyncP()
        {
            return await _ReservationPiontsCollection.Find(new BsonDocument()).ToListAsync();
        }
        public async Task<ReservationPoint> GetAsyncP(string id)
        {
            FilterDefinition<ReservationPoint> filter = Builders<ReservationPoint>.Filter.Where(x => x.Id == id);
            ReservationPoint ret = await _ReservationPiontsCollection.Find(filter).FirstOrDefaultAsync();
            return ret;
        }
        public async Task<bool> UpdateAsyncP(ReservationPoint p)
        {
            if (p == null) return false;
            if (string.IsNullOrEmpty(p.Id))
            {
                //create new RP in collection "reservation points"
                await _ReservationPiontsCollection.InsertOneAsync(p);
                return true;
            }
            else
            {
                //update existing RP in collection "reservation points"
                FilterDefinition<ReservationPoint> filter = Builders<ReservationPoint>.Filter.Eq("Id", p.Id);
                UpdateDefinition<ReservationPoint> update = Builders<ReservationPoint>.Update.Set("Name", p.Name);
                UpdateResult r =  await _ReservationPiontsCollection.UpdateOneAsync(filter, update);
                bool ret = r.ModifiedCount > 0 ? true : false;
                return ret;
            }

            return false;
        }
        public bool DeleteP(string id)
        {

            DeleteResult d = _ReservationPiontsCollection.DeleteOne(x => x.Id == id);
            bool ret = d.DeletedCount > 0;
            return ret;
        }
        public async Task<int> DeleteOverdueAsync(DateTime? deadline = null)
        {
            DateTime Deadline = deadline ?? TimeZoneInfo.ConvertTime(DateTime.Now, TimeZone);
            Deadline = new DateTime(Deadline.Year, Deadline.Month, Deadline.Day);   //set hour to zero
            Deadline = DateTime.SpecifyKind(Deadline, DateTimeKind.Utc);            //mark CET as if it were UTC to prevent further conversion by Mongo
            List<Reservation> ReservationsList = await _ReservationsCollection.Find(x => (x.DateT < Deadline) ).ToListAsync();

            int ret = await DeleteReservations(ReservationsList);
            return ret;
        }
        public async Task<int> DeleteReservationsByUserId(string userId)
        {
            List<Reservation> ReservationsList = await _ReservationsCollection.Find(x => (x.UserId == userId)).ToListAsync();
            int ret = await DeleteReservations(ReservationsList);
            return ret;
    
        }
        public async Task<int> DeleteReservationsByPoint(string rpId)
        {
            List<Reservation> ReservationsList = await _ReservationsCollection.Find(x => (x.RpId == rpId)).ToListAsync();
            int ret = await DeleteReservations(ReservationsList);
            return ret;
        }
        protected async Task<int> DeleteReservations(List<Reservation> reservationsList)
        {
            IEnumerable<IGrouping<string, Reservation>> Refs1 = reservationsList.GroupBy(x => x.RpId);
            int ret = Refs1.Count();
            foreach (IGrouping<string, Reservation> g in Refs1)
            {
                string RpId = g.Key;
                List<Reservation> r = g.Distinct().ToList();
                List<string> rids = r.Select(x => x.Id).ToList();

                FilterDefinition<ReservationPoint> filter = Builders<ReservationPoint>.Filter.Eq("Id", RpId);
                UpdateDefinition<ReservationPoint> update = Builders<ReservationPoint>.Update.PullAll(x => x.ReservationIDs, rids);
                await _ReservationPiontsCollection.UpdateOneAsync(filter, update);

            }
            FilterDefinition<Reservation> filtron = Builders<Reservation>.Filter.In("Id", reservationsList.Select(x => x.Id).ToList());
            _ReservationsCollection.DeleteMany(filtron);
            return ret;
        }

        public async Task<Reservation> GetAsyncR(string id)
        {
            IAsyncCursor<Reservation> cret = await _ReservationsCollection.FindAsync(x => x.Id == id);
            Reservation ret = cret.FirstOrDefault();
            return ret;
        }

        public async Task<List<ReservationDay>> GetAsyncR(DateOnly dateStart, DateOnly dateEnd) 
        {
            DateTime dtStart = new DateTime(dateStart.Year, dateStart.Month, dateStart.Day, 0, 0, 0);
            DateTime dtEnd = new DateTime(dateEnd.Year, dateEnd.Month, dateEnd.Day, 23, 59, 59);

            IList<DateTime> dateInterval = new List<DateTime>();
            for (DateTime date = dtStart; date < dtEnd; date = date.AddDays(1))
            {
                DateTime dt = new DateTime(date.Year, date.Month, date.Day);
                dateInterval.Add(dt);
            }

            var filtron = Builders<Reservation>.Filter;
            FilterDefinition<Reservation> filter = filtron.And(filtron.Gte(r => r.DateT, dtStart), filtron.Lte(r => r.DateT, dtEnd));
            IList <Reservation> ReservationsCollection  = await _ReservationsCollection.Find(filter).ToListAsync();

            //ReservationsCollection.GroupBy(x => x.RpId).Select(x => x.Select(y => y).ToList()).ToList();

            List<List<Reservation>> uret = ReservationsCollection.GroupBy(x => x.DateT).Select(x => x.Select(y => y).ToList()).ToList();

            List <ReservationDay> ret = new List<ReservationDay>();
            foreach( DateTime d in dateInterval)
            {
                List<Reservation> ress = uret.FirstOrDefault(x => ( x?.ElementAt(0).DateT.Year == d.Year && x?.ElementAt(0).DateT.Month == d.Month && x?.ElementAt(0).DateT.Day == d.Day ));

                ret.Add( new ReservationDay(DateOnly.FromDateTime(d), ress ?? new List<Reservation>()));
            }

            return ret;
        }
        public async Task<List<Reservation>> GetAsyncR()
        {
            IAsyncCursor<Reservation> acret = await _ReservationsCollection.FindAsync(x => true);
            List<Reservation> ret = await acret.ToListAsync();
            return ret;
        }
        public async Task<Reservation> GetAsyncR(string pointId, DateTime day)
        {
            //normalize_date
            //List<Reservation> retlist = await _ReservationsCollection.Find(x => (x.RpId == pointId && x.DateT.Year == day.Year && x.DateT.Month == day.Month && x.DateT.Day == day.Day)).ToListAsync();
            List<Reservation> retlist = await _ReservationsCollection.Find(x => x.RpId == pointId ).ToListAsync();
            Reservation ret = retlist?.Where(x => x.DateT.Year == day.Year && x.DateT.Month == day.Month && x.DateT.Day == day.Day).FirstOrDefault();
            return ret;
        }
        public Reservation GetR(string pointId, DateTime day)
        {
            //normalize_date
            Reservation ret = _ReservationsCollection.Find(x => (x.RpId == pointId && x.DateT.Year == day.Year && x.DateT.Month == day.Month && x.DateT.Day == day.Day)).ToList().FirstOrDefault();
            return ret;
        }
    }
}
