import { Meteor } from 'meteor/meteor';
import { Mongo } from 'meteor/mongo';


export const Geometry = new Mongo.Collection('geometry');

if (Meteor.isServer) {
  Meteor.publish('cube', function cubePublication() {
    return Geometry.find({type: 'cube'});
  });
  Meteor.publish('sphere', function spherePublication() {
    return Geometry.find({type: 'sphere'});
  });
  Meteor.publish('monkey', function spherePublication() {
    return Geometry.find({type: 'monkey'});
  });
}
