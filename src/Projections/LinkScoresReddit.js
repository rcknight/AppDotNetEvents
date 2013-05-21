///this is the reddit algorithm described here
/// http://amix.dk/blog/post/19588
///

fromStream('PostsWithLinks')
.when({
    $init: function () {
        return {
            totalLinks: 0,
            links: {}
            //links will look like: { url: ...,total: 0, age: 0, score: 100 }
        };
    },

    AppDotNetPost: function (s, e) {
        var links = e.body.entities.links;
        var postDate = new Date(e.body.created_at);
        var secondsSinceEpoch = postDate.getTime() / 1000;
        postDate.setUTCMinutes(0, 0, 0);

        if (s.currentHour === undefined)
            s.currentHour = postDate.toString();

        //if we moved on an hour, remove any links with only 1 post, to keep size of state down
        if (postDate.toString() != s.currentHour) {
            removeUnpopular(s);
            s.currentHour = postDate.toString();
        }

        for (var i = 0; i < links.length; i++) {
            if (links[i].url !== undefined)
                processLink(s, encodeURIComponent(links[i].url), secondsSinceEpoch);
        }
    }

}).transformBy(function (s) {
    var sortedArray = sortObject(s.links);
    if (sortedArray.length > 25)
        return { links: sortedArray.slice(-25) };
    return { links: sortedArray };
});

//post time is seconds since epoch
function processLink(s, l, postTime) {
    //random reddit constant, dont know if i need this?
    postTime = postTime - 1134028003;
    if (s.links[l.toLowerCase()] === undefined) {
        s.links[l.toLowerCase()] = { url: l, total: 1, age: postTime, score: 0 };
        s.totalLinks++;
    }
    //add one to the newest count
    s.links[l.toLowerCase()].total++;
    score(s.links[l.toLowerCase()]);
}

function removeUnpopular(s) {
    var sortedArray = sortObject(s.links);
    var amountToRemove = sortedArray.length - 300;

    if (amountToRemove <= 0)
        return;

    var toRemove = sortedArray.slice(0, amountToRemove);
    for (var i = 0; i < toRemove.length; i++) {
        delete s.links[toRemove[i].url.toLowerCase()];
        s.totalLinks--;
    }
}

function score(l) {
    l.score = log10(l.total) + (l.age / 45000);
}

function log10(val) {
    return Math.log(val) / Math.LN10;
}

function sortObject(obj) {
    var arr = [];
    for (var prop in obj) {
        if (obj.hasOwnProperty(prop)) {
            arr.push({
                'score': obj[prop].score,
                'url': obj[prop].url
            });
        }
    }
    arr.sort(function (a, b) { return a.score - b.score; });
    return arr; // returns array
}