///this is the old hackernews sorting algorithm
/// http://amix.dk/blog/post/19574
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
        postDate.setUTCMinutes(0, 0, 0);

        if (s.currentHour === undefined)
            s.currentHour = postDate.toString();

        //if we moved on an hour, remove any links with only 1 post, to keep size of state down
        if (postDate.toString() != s.currentHour) {
            //removeUnpopular(s);
            s.currentHour = postDate.toString();
            incrementAndScore(s.links);
        }

        for (var i = 0; i < links.length; i++) {
            if (links[i].url !== undefined)
                processLink(s, encodeURIComponent(links[i].url));
        }
    }
    
}).transformBy(function (s) {
    var sortedArray = sortObject(s.links);
    if (sortedArray.length > 25)
        return { links: sortedArray.slice(-25) };

    return { links: sortedArray };
});

function processLink(s, l) {
    //initial state with 24 0s
    if (s.links[l] === undefined) {
        s.links[l] = { url: l, total: 0, age: 0, score: 0 };
        s.totalLinks++;
    }
    //add one to the newest count
    s.links[l].total++;
    score(s.links[l]);
}

function removeUnpopular(s) {
    var allLinks = s.links;
    for (var key in allLinks) {
        if (allLinks.hasOwnProperty(key)) {
            if (allLinks[key].score < 5) {
                delete allLinks[key];
                s.totalLinks--;
            }
        }
    }
}

function score(l) {
    l.score = ((l.total - 1) / Math.pow(l.age + 2, 1.8));
}

function incrementAndScore(allLinks) {
    for (var key in allLinks) {
        if (allLinks.hasOwnProperty(key)) {
            allLinks[key].age++;
            score(allLinks[key]);
        }
    }
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