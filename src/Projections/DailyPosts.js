/*
Partitions posts by month, then maintains a count of posts for each day.
*/

fromAll()
.partitionBy(function (e) {
    if (e.body !== undefined) {
        var date = new Date(e.body.created_at);
        return date.getUTCFullYear() + '' + date.getUTCMonth();
    }
})
.when({
    AppDotNetPost: function (s, e) {
        //some posts dont't parse correctly, just ignore them
        if (e.body === undefined)
            return;

        var date = new Date(e.body.created_at);
        var day = date.getUTCDate();
        if (s[day] === undefined)
            s[day] = 0;
        s[day]++;
    }
});