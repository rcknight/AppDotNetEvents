fromAll()
.when({
    $init: function () {
        return {
            lastDates: ["", "", "", "", "", "", ""],
            daysSeen: [0, 0, 0, 0, 0, 0, 0],
            dailyTotals: [0, 0, 0, 0, 0, 0, 0],
            dailyAverages: [0, 0, 0, 0, 0, 0, 0]
        };
    },
    AppDotNetPost: function (s, e) {
        //some posts are weird and dont parse right
        if(e.body === undefined)
        {
            return;
        }
        var date = new Date(e.body.created_at);
        var dayOfWeek = date.getUTCDay();
        date.setUTCHours(0, 0, 0, 0);
        
        if (s.lastDates[dayOfWeek] !== date.toString()) {
            s.lastDates[dayOfWeek] = date.toString();
            s.daysSeen[dayOfWeek]++;
        }

        s.dailyTotals[dayOfWeek]++;
        s.dailyAverages[dayOfWeek] = Math.round(s.dailyTotals[dayOfWeek] / s.daysSeen[dayOfWeek]);
    }
});