// Gulp and package
const {src, dest, parallel, series, watch} = require('gulp');

// Plugins
const autoprefixer = require('autoprefixer');
const concat = require('gulp-concat');
const cssnano = require('cssnano');
const pixrem = require('pixrem');
const plumber = require('gulp-plumber');
const postcss = require('gulp-postcss');
const rename = require('gulp-rename');
const npmdist = require('gulp-npm-dist');
const replace = require('gulp-replace');
const gulUglifyES = require('gulp-uglify-es');
const gulpSass = require('gulp-sass');
const dartSass = require('sass');
const tildeImporter = require('node-sass-tilde-importer');
const sass = gulpSass(dartSass);
const uglify = gulUglifyES.default;
const pluginFile = require("./plugins.config"); // Import the plugins list


const paths = {
    baseDistAssets: "wwwroot/",  // build assets directory
    baseSrcAssets: "wwwroot/",   // source assets directory
};
// Copying Third Party Plugins Assets
const plugins = function () {
    const out = paths.baseDistAssets + "/plugins/";

    pluginFile.forEach(({name, vendorsJS, vendorCSS, vendorFonts, assets, fonts, font, media, img, webfonts}) => {

        const handleError = (label, files) => (err) => {
            const shortMsg = err.message.split('\n')[0];
            console.error(`\n${label} - ${shortMsg}`);
            throw new Error(`${label} failed`);
        };

        if (vendorsJS) {
            src(vendorsJS)
                .on('error', handleError('vendorsJS'))
                .pipe(concat("vendors.min.js"))
                .pipe(dest(paths.baseDistAssets + "/js/"));
        }

        if (vendorCSS) {
            src(vendorCSS)
              .pipe(concat("vendors.min.css"))
              .on('error', handleError('vendorCSS'))
              .pipe(replace(/url\((['"]?)(remixicon|boxicons)/g, "url($1fonts/$2"))
              .pipe(dest(paths.baseDistAssets + "/css/"));
        }

        if (vendorFonts) {
            src(vendorFonts)
                .on('error', handleError('vendorFonts'))
                .pipe(dest(paths.baseDistAssets + "/css/fonts/"));
        }

        if (assets) {
            src(assets)
                .on('error', handleError('assets'))
                .pipe(dest(`${out}${name}/`));
        }

        if (img) {
            src(img)
                .on('error', handleError('img'))
                .pipe(dest(`${out}${name}/images/`));
        }

        if (media) {
            src(media)
                .on('error', handleError('media'))
                .pipe(dest(`${out}${name}/`));
        }


        if (fonts) {
            src(fonts)
                .on('error', handleError('fonts'))
                .pipe(dest(`${out}${name}/fonts/`));
        }

        if (font) {
            src(font)
                .on('error', handleError('font'))
                .pipe(dest(`${out}${name}/font/`));
        }

        if (webfonts) {
            src(webfonts)
                .on('error', handleError('webfonts'))
                .pipe(dest(`${out}${name}/webfonts/`));
        }
    });

    return Promise.resolve();
};

const processCss = [
    autoprefixer(), // adds vendor prefixes
    pixrem(), // add fallbacks for rem units
];

const minifyCss = [
    cssnano({preset: 'default'}), // minify result
];

const styles = function () {
    const out = paths.baseDistAssets + "/css/";

    return src(paths.baseSrcAssets + "/scss/**/*.scss")
        .pipe(
            sass({
                importer: tildeImporter,
                includePaths: [paths.baseSrcAssets + "/scss"],
            }).on('error', sass.logError),
        )
        .pipe(plumber()) // Checks for errors
        .pipe(postcss(processCss))
        .pipe(dest(out))
        .pipe(rename({suffix: '.min'}))
        .pipe(postcss(minifyCss)) // Minifies the result
        .pipe(dest(out));
};

function watchFiles() {
    watch(paths.baseSrcAssets + "/scss/**/*.scss", series(styles));
}


// Production Tasks
exports.default = series(
    plugins,
    parallel(styles),
    parallel(watchFiles)
);

// Build Tasks
exports.build = series(
    plugins,
    parallel(styles),
);
