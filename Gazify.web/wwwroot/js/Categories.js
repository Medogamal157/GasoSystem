//handle toggle status
$('body').delegate('.js-toggle-status', 'click', function () {
    var btn = $(this);

    bootbox.confirm({
        message: 'Are you sure that you need to toggle this item status?',
        buttons: {
            confirm: {
                label: 'Yes',
                className: 'btn-danger'
            },
            cancel: {
                label: 'No',
                className: 'btn-secondary'
            }
        },
        callback: function (result) {
            if (result) {
                $.post({
                    url: btn.data('url'),
                    data: {
                        '__RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
                    },
                    success: function (lastUpdatedOn) {
                        var row = btn.parents('tr');
                        var status = row.find('.js-status');
                        var newStatus = status.text().trim() === 'Deleted' ? 'Available' : 'Deleted';
                        status.text(newStatus).toggleClass('badge-light-success badge-light-danger');
                        row.find('.js-updated-on').html(lastUpdatedOn);
                        row.addClass('animate__animated animate__flash');

                        showSuccessMessage();
                    },
                    error: function () {
                        showErrorMessage();
                    }
                });
            }
        }
    });
});