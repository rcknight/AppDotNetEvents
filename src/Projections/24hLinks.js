fromStream('PostsWithLinks')
.when({
    $init: function () {
        return {
            totalLinks: 0,
            links: {}
            //links will look like: { url: ..., total: 0, counts: [..1 per hour..] }
        };
    },

    AppDotNetPost: function (s, e) {
        var links = e.body.entities.links;

        var postDate = new Date(e.body.created_at);
        postDate.setUTCMinutes(0, 0, 0);

        if (s.currentHour === undefined)
            s.currentHour = postDate.toString();
        
        //if we moved on an hour, rotate ALL the arrays!
        if (postDate.toString() != s.currentHour) {
            rotateLinkCounts(s);
            s.currentHour = postDate.toString();
        }
            
        
        for (var i = 0; i < links.length; i++) {
            if (links[i].url !== undefined)
                processLink(s, encodeURIComponent(links[i].url));
        }
    }
}).transformBy(function(s) {
    var sortedArray = sortObject(s.links);
    if (sortedArray.length > 25)
        return { links: sortedArray.slice(-25) };

    return { links: sortedArray };
});

function rotateLinkCounts(s) {
    var allLinks = s.links;
    for (var key in allLinks) {
        if (allLinks.hasOwnProperty(key)) {
            removeOldest(allLinks[key]);
            if (allLinks[key].total < 3) {
                delete allLinks[key];
                s.totalLinks--;
            }
        }
    }
}

function processLink(s, l) {
    //initial state with 24 0s
    if (s.links[l] === undefined) {
        s.links[l] = { url: l, total: 0, counts: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0] };
        s.totalLinks++;
    }

    //add one to the newest count
    s.links[l].counts[23]++;
    s.links[l].total++;
}

function removeOldest(l) {
    //remove the first element
    l.total -= l.counts[0];
    l.counts.push(0);
    l.counts.shift();
}

function sortObject(obj) {
    var arr = [];
    for (var prop in obj) {
        if (obj.hasOwnProperty(prop)) {
            arr.push({
                'total': obj[prop].total,
                'url': obj[prop].url
            });
        }
    }
    arr.sort(function (a, b) { return a.total - b.total; });
    return arr; // returns array
}


