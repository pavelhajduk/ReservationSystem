using MongoDB.Driver.Core.Operations;
using System;
using System.Collections;
using System.Threading.Tasks;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text;
using System.Text.Json.Serialization;
using System.Linq;
using MailKit.Net.Imap;
using System.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Routing;
using System.Text.RegularExpressions;

namespace PSRes.Models
{
    public class ReservationPoint
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        
        [BsonElement("name")]
        [JsonPropertyName("name")]
        public string Name { get; set; } = null!;

        [BsonElement("reservations")]
        [JsonPropertyName("reservations")]
        public List<string> ReservationIDs { get; set; } = null!;
    }
    public class Reservation
    {

        public Reservation(string userID, string rpId, DateOnly date)
        {
            UserId = userID;
            RpId = rpId;
            DateT = new DateTime(date.Year, date.Month, date.Day, 12, 0, 0);
        }

        [BsonIgnore]
        public DateOnly Date { get { return DateOnly.FromDateTime(DateT); } set { DateT = new DateTime(value.Year, value.Month, value.Day); } }

        
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonElement("userid")]
        [JsonPropertyName("userid")]
        [BsonRepresentation(BsonType.String)]
        public string UserId { get; set; } = null!;

        [BsonElement("rpid")]
        [JsonPropertyName("userid")]
        [BsonRepresentation(BsonType.String)]
        public string RpId   { get; set; } = null!;

        [BsonElement("date")]
        [BsonRepresentation(BsonType.DateTime)]
        public DateTime DateT { get; set; }

    }


    public class ReservationDay
    {
        public DateOnly date;
        public List<Reservation> Reservations;
        public Reservation GetReservation(ReservationPoint point) { return Reservations.FirstOrDefault(i => i.RpId == point.Id); }

        public ReservationDay(DateOnly date, List<Reservation> res)
        {
            this.date = date;
            this.Reservations = res;

        }
    }

    public class ReservationWindow
    {
        protected List<ReservationDay> Days;
        protected List<ReservationPoint> Points;
        protected string UserId { get; set; }

        public string Note;

        public ReservationWindow() { WeekOffset = 0; WeeksDisplayed = 2; }
        public ReservationWindow(List<ReservationPoint> points, List<ReservationDay> days, int offset, string userId = null)
        {
            ReInitiate(points, days, userId, offset);
        }
        public int WeeksDisplayed { get; set; }
        public int WeekOffset { get; set; }

        public void ReInitiate(List<ReservationPoint> points, List<ReservationDay> days, string userId, int offset, int windowSpan = 2)
        {
            Days = days.OrderBy(i => i.date).ToList();
            Points = points;
            UserId = userId;
            WeekOffset = offset;
            WeeksDisplayed = windowSpan;

            DateOnly dateStart = DateOnly.FromDateTime(DateTime.Now).AddDays(offset * 7);
            DateOnly dateEnd = dateStart.AddDays(windowSpan * 7);
            int ct = 0;
            for (DateOnly date = dateStart; date <= dateEnd; date = date.AddDays(1))
            {
                if (days.Find(i => i.date == date) == null)
                {
                    if (ct >= days.Count) days.Add(new ReservationDay(date, new List<Reservation>()));
                    else days.Insert(ct, new ReservationDay(date, new List<Reservation>()));
                }
                ct++;
            }
        }
        public Reservation ReservationAt(int nRow, int nCol, string userId = null)
        {
            if (nCol >= Days.Count   || nCol < 0) return null;
            if (nRow >= Points.Count || nRow < 0) return null;

            ReservationPoint p = Points[nRow];
            if (p == null) return null;
            ReservationDay d = Days[nCol];
            if (d == null) return null;
            Reservation r = d.Reservations.FirstOrDefault(s => s.RpId == p.Id);
            if (string.IsNullOrEmpty(userId) ||  r != null) return r;
            return new Reservation(userId, p.Id, d.date);
        }
        public DateOnly DateAt(int col)
        {
            DateTime dt = DateTime.Now;
            DateOnly dol = DateOnly.FromDateTime(dt);
            if (col < 0 || col >= Days.Count) return dol;
            return Days.ElementAt(col).date;
        }

        public HtmlString GenerateGrid()
        {
            DateTime baseDateTime = DateTime.Now.AddDays(WeekOffset * 7);
            DateOnly baseDate = DateOnly.FromDateTime(baseDateTime);
            List<ReservationDay> sortedDays = Days.OrderBy(i => i.date).ToList();
            

            //generate grid


            //generate string
            StringBuilder sb = new StringBuilder();
            Reservation res;
            //sb.Append("<form method=\"post\">");
            sb.Append("<table>");

            sb.Append("<tr style=\"border: 1px solid #dddddd;\">");
            sb.Append("<th><img src=\"/Images/menu.png\" width=\"100\" height=\"50\"/><th/>");
            foreach (ReservationDay day in sortedDays)
            {
                sb.Append($"<th style=\"border-spacing: 0 10px\"><h6> {day.date.Day.ToString()}.{day.date.Month.ToString()}</h6></th>");
            }

            int nColumnID = 0;
            int nRowID = 0;

            sb.Append("</tr>");
            foreach (ReservationPoint point in Points)
            {
                nColumnID = 0;
                sb.Append("<tr>");
                sb.Append($"<th width=\"200\" height=\"50\" background=\"/Images/sitename.png\"><br/>{point.Name}<th/>");
                foreach (ReservationDay day in sortedDays)
                {
                    sb.Append("<td>");
                    sb.Append($"<button class=\"btn btn-primary\" type=\"submit\" name=\"button\" value=\"{GridButtonValue(nRowID, nColumnID)}\">");
                    if ((res = day.GetReservation(point)) != null) {
                        //sb.Append("<img src =\"/Images/on.png\" width=\"50\" height=\"50\" />");
                        int level = (UserId != res.UserId) ? 2 : 1;
                        //int level = 1;
                        sb.Append($"<img src ={ImgSrc(level)} width=\"50\" height=\"50\" />");
                        
                    }
                    else
                    {
                        //sb.Append("<img src =\"/Images/off.png\" width=\"50\" height=\"50\" />");
                        sb.Append($"<img src ={ImgSrc(0)} width=\"50\" height=\"50\" />");
                    }
                    sb.Append("</button>");
                    sb.Append("</td>");
                    nColumnID++;
                }
                sb.Append("</tr>");
                nRowID++;
            }
            sb.Append("</table>");
            //sb.Append("</form>");

            HtmlString s = new HtmlString(sb.ToString());
            return s;
        }
        public static HtmlString ImgSrc(int level, bool quoted = true)
        {
            StringBuilder sb = new StringBuilder();
            
            switch(level) { 
                case 0: //vacant
                    sb.Append("\"/Images/off.png\"");
                    break;
                case 1: //reserved by me
                    sb.Append("\"/Images/mine.png\"");
                    break;
                case 2: //reserved by someone else
                    sb.Append("\"/Images/on.png\"");
                    break;
            }
            if (!quoted) sb.Replace('\"', ' ');
            string ss = sb.ToString().Trim();
            HtmlString ret = new HtmlString(ss);
            return ret;
        }


        public string GridButtonValue(int nRow, int nCol)
        {
            return $"S_o{WeekOffset}_i{WeeksDisplayed}_c{nCol}_r{nRow}";
        }
        public string ArrowButtonValue(bool bForward)
        {
            int Direction = bForward ? 1 : 0;
            return $"M_o{WeekOffset}_i{WeeksDisplayed}_d{Direction}";
        }
        static public (int, int, int, int) ParseGridButtonValue(string idstring)
        {
            if (idstring[0] != 'S') return (-1, -1, -1, -1);
            Match m = Regex.Match(idstring, @"_r\d+");
            if (!m.Success) return (-1, -1, -1, -1);
            int nRow = int.Parse(new String(m.Value.Where(char.IsDigit).ToArray()));
            m = Regex.Match(idstring, @"_c\d+");
            if (!m.Success) return (-1, -1, -1, -1);
            int nCol = int.Parse(new String(m.Value.Where(char.IsDigit).ToArray()));

            int nInterval = 2;
            m = Regex.Match(idstring, @"_i\d+");
            if (m.Success)
            {
                nInterval = int.Parse(new String(m.Value.Where(char.IsDigit).ToArray()));
            }
            int nOffset = 0;
            m = Regex.Match(idstring, @"_o\d+");
            if (m.Success)
            {
                nOffset = int.Parse(new String(m.Value.Where(char.IsDigit).ToArray()));
            }
            
            //nOffset *= nInterval * 7;
            return (nRow, nCol, nOffset, nInterval);

        }
        static public (int, int) ParseArrowButtonValue(string idstring)
        {
            if (idstring[0] != 'M') return (-1, -1);
            Match m = Regex.Match(idstring, @"_d\d+");
            if (!m.Success) return (-1, -1);
            int nDir = int.Parse(new String(m.Value.Where(char.IsDigit).ToArray()));
            nDir = nDir == 0 ? -1 : 1;
            
            int nInterval = 2;
            m = Regex.Match(idstring, @"_i\d+");
            if (m.Success)
            {
                nInterval = int.Parse(new String(m.Value.Where(char.IsDigit).ToArray()));
            }
            int nOffset = 0;
            m = Regex.Match(idstring, @"_o\d+");
            if (m.Success)
            {
                nOffset = int.Parse(new String(m.Value.Where(char.IsDigit).ToArray()));
            }

            nOffset += nDir;

            return (nOffset, nInterval);
        }

        //continue here
    }

}
