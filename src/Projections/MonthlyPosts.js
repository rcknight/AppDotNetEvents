/*
Partitions posts by year, then maintains a count of posts for each month.
*/

fromAll()
.partitionBy(function (e) {
    if (e.body !== undefined) {
        var date = new Date(e.body.created_at);
        return date.getUTCFullYear().toString();
    }
})
.when({
    AppDotNetPost: function (s, e) {
        //some posts dont't parse correctly, just ignore them
        if (e.body === undefined)
            return;

        var date = new Date(e.body.created_at);
        var month = date.getUTCMonth();
        if (s[month] === undefined)
            s[month] = 0;
        s[month]++;
    }
});