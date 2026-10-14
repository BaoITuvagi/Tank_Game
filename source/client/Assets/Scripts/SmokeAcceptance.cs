namespace Tank  {
    public static class SmokeAcceptance  {
        public static bool Passed(ClientCore client,int expectedTicks) {
            return expectedTicks>0&&client.Ended&&client.EndReason==0&&client.Match!=0&&client.Invalid==0&& client.Latest!=null&&client.Latest.Tick>=expectedTicks-1&&client.Decoded>=System.Math.Max(1,expectedTicks*95/100)&& client.History.Exists(h=>h.Match==client.Match&&h.Reason==0);
        }
    }
}
