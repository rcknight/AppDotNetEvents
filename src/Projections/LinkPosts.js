fromAll()
.when({
    $init: function () {
        return { count: 0 }; // initial state
    },

    AppDotNetPost: function (s, e) {
        if (e.body !== undefined && e.body.entities !== undefined && e.body.entities.links !== undefined) {
            if (e.body.entities.links.length !== 0) {
                //there are some links in this post
                s.count += 1;
                linkTo('WithLinks', e);
            }
        }
    }
});