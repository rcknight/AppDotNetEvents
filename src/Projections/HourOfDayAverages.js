fromAll()
    .when({
        $init: function() {
            return {
                lastDates: [],
                daysSeen: [],
                hourlyTotals: [],
                hourlyAverages: []
            };
        },
        AppDotNetPost: function(s, e) {
            //some posts are weird and dont parse right
            if (e.body === undefined) {
                return;
            }
            var date = new Date(e.body.created_at);

            var hourOfDay = date.getUTCHours();
            date.setUTCHours(0, 0, 0, 0);

            if (s.lastDates[hourOfDay] !== date.toString()) {
                s.lastDates[hourOfDay] = date.toString();
                if (s.daysSeen[hourOfDay] === undefined)
                    s.daysSeen[hourOfDay] = 0;
                s.daysSeen[hourOfDay]++;
            }

            if (s.hourlyTotals[hourOfDay] === undefined)
                s.hourlyTotals[hourOfDay] = 0;

            s.hourlyTotals[hourOfDay]++;
            s.hourlyAverages[hourOfDay] = Math.round(s.hourlyTotals[hourOfDay] / s.daysSeen[hourOfDay]);
        }
    })
    .transformBy(function(s) {
        return s.hourlyAverages;
    });