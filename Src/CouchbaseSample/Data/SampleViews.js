//all_breweries::map
function (doc) {
  if (doc.type == "brewery") {
	emit(doc._id, doc);
  }
}

//beers_by_name::map
function (doc) {
    if (doc.type == "beer") {
        emit(doc.name, doc);
    }
}

//beers_by_abv::map
function (doc) {
    if (doc.type == "beer") {
        emit(doc.abv, doc);
    }
}

//beers_by_name_and_abv::map
function (doc) {
    if (doc.type == "beer") {
        emit([doc.name, doc.ABV], doc);
    }
}

//all_beers::map
function (doc) {
    if (doc.type == "beer") {
        emit(doc.id, doc)
    }
}
