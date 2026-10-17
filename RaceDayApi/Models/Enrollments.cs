namespace RaceDayApi.Models
{//start of namespace

    public class Enrollments
    {//start of class

        //declaring the data of enrollments tables
        public int enrollmentID { get; set; }

        public int RaceNumber { get; set; }

        public string DateEnrolled { get; set; }

        public string PaymentStatus { get; set; }

        public int participantID { get; set; }

        public int categoryID { get; set; }




    }//end of class

}//end of namespace 
