import { Meteor } from 'meteor/meteor';
import { Mongo } from 'meteor/mongo';


export const Geometry = new Mongo.Collection('geometry');

if (Meteor.isServer) {
  Meteor.publish('geometry', function notesPublication() {
    return Geometry.find({});
  });
}
